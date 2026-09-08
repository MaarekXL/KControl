#include <Arduino.h>
#include <DNSServer.h>
#include <ESPmDNS.h>
#include <Preferences.h>
#include <WebServer.h>
#include <WiFi.h>
#include <WiFiUdp.h>

#include <cmath>
#include <cstdlib>
#include <cstring>

#include <esp_display_panel.hpp>
#include <esp_err.h>
#include <lvgl.h>

#include "esp_lv_adapter_arduino.h"

using namespace esp_panel::board;
using namespace esp_panel::drivers;

namespace {

// Temporary hardware diagnostic: keep the panel on the driver's native
// colour-bar test before LVGL or Wi-Fi are started. Set to false once the
// physical RGB panel path has been validated.
constexpr bool kPanelColorBarDiagnostic = false;

constexpr uint16_t kTelemetryPort = 42100;
constexpr uint32_t kTelemetryTimeoutMs = 10'000;
constexpr uint32_t kWifiConnectTimeoutMs = 15'000;
constexpr uint32_t kWifiRetryEveryMs = 10'000;
constexpr uint32_t kPortalAfterOfflineMs = 90'000;
constexpr char kPortalPassword[] = "keryxdisplay";

lv_color_t color(uint32_t hex)
{
    return lv_color_hex(hex);
}

enum class UiState {
    Booting,
    WifiSetup,
    Waiting,
    Mining,
    Stopped,
    Warning,
    Offline,
};

struct Telemetry {
    uint32_t sequence = 0;
    char state[20] = "waiting";
    uint8_t gpuCount = 0;
    double hashRateHs = 0.0;
    float maxTemperatureC = 0.0F;
    float totalPowerW = 0.0F;
    uint64_t accepted = 0;
    uint64_t rejected = 0;
    uint64_t uptimeSeconds = 0;
    char miner[32] = "Keryx Manager";
};

Preferences preferences;
WiFiUDP udp;
DNSServer dnsServer;
WebServer webServer(80);

bool portalActive = false;
bool restartPending = false;
bool telemetryIsStale = true;
uint32_t restartAtMs = 0;
uint32_t lastTelemetryMs = 0;
uint32_t disconnectedSinceMs = 0;
uint32_t lastWifiRetryMs = 0;
uint32_t udpPacketCount = 0;
uint32_t udpInvalidPacketCount = 0;
uint32_t udpRenderedPacketCount = 0;
String portalSsid;
String hostName;

lv_obj_t *statusPill = nullptr;
lv_obj_t *statusDot = nullptr;
lv_obj_t *statusLabel = nullptr;
lv_obj_t *hashValueLabel = nullptr;
lv_obj_t *hashUnitLabel = nullptr;
lv_obj_t *temperatureValueLabel = nullptr;
lv_obj_t *powerValueLabel = nullptr;
lv_obj_t *gpuValueLabel = nullptr;
lv_obj_t *sharesValueLabel = nullptr;
lv_obj_t *uptimeValueLabel = nullptr;
lv_obj_t *footerLabel = nullptr;

const char kPortalHtml[] PROGMEM = R"HTML(
<!doctype html><html lang="fr"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Keryx Display</title><style>
:root{color-scheme:dark}body{margin:0;background:#050b08;color:#eaf7ef;font-family:Segoe UI,Arial,sans-serif}
main{max-width:520px;margin:7vh auto;padding:24px}.logo{color:#19e778;font-weight:800;letter-spacing:.06em}
.card{margin-top:22px;background:#0b1510;border:1px solid #173824;border-radius:18px;padding:24px;box-shadow:0 16px 50px #0008}
h1{font-size:28px;margin:8px 0 4px}p{color:#9ab5a4;line-height:1.5}label{display:block;margin-top:18px;color:#b8d2c1;font-size:14px}
input{box-sizing:border-box;width:100%;margin-top:7px;padding:13px;border-radius:10px;border:1px solid #28533a;background:#07100b;color:white;font-size:16px}
button{width:100%;margin-top:24px;padding:14px;border:0;border-radius:10px;background:#19e778;color:#031108;font-weight:800;font-size:16px}
.note{font-size:13px;color:#6f8d79}</style></head><body><main><div class="logo">KERYX DISPLAY</div>
<section class="card"><h1>Connexion Wi-Fi</h1><p>Entrez le reseau utilise par le PC qui execute Keryx Manager.</p>
<form method="post" action="/save"><label>Nom du reseau (SSID)</label><input name="ssid" maxlength="32" required autocomplete="off">
<label>Mot de passe Wi-Fi</label><input name="password" type="password" maxlength="64" autocomplete="new-password">
<button type="submit">ENREGISTRER ET REDÉMARRER</button></form>
<p class="note">L'adresse Keryx et les identifiants du mineur ne sont jamais transmis a l'ecran.</p></section></main></body></html>
)HTML";

void stylePanel(lv_obj_t *object, uint32_t background, uint32_t border, int radius = 14)
{
    lv_obj_set_style_bg_color(object, color(background), 0);
    lv_obj_set_style_bg_opa(object, LV_OPA_COVER, 0);
    lv_obj_set_style_border_color(object, color(border), 0);
    lv_obj_set_style_border_width(object, 1, 0);
    lv_obj_set_style_radius(object, radius, 0);
    lv_obj_set_style_pad_all(object, 0, 0);
    lv_obj_clear_flag(object, LV_OBJ_FLAG_SCROLLABLE);
}

lv_obj_t *makeText(lv_obj_t *parent, const char *text, int x, int y, const lv_font_t *font, uint32_t textColor)
{
    lv_obj_t *label = lv_label_create(parent);
    lv_label_set_text(label, text);
    lv_obj_set_pos(label, x, y);
    lv_obj_set_style_text_font(label, font, 0);
    lv_obj_set_style_text_color(label, color(textColor), 0);
    return label;
}

lv_obj_t *makeCard(lv_obj_t *parent, int x, int y, int width, int height, const char *caption)
{
    lv_obj_t *card = lv_obj_create(parent);
    lv_obj_set_pos(card, x, y);
    lv_obj_set_size(card, width, height);
    stylePanel(card, 0x0A130E, 0x173824);
    makeText(card, caption, 16, 12, &lv_font_montserrat_16, 0x63A57B);
    return card;
}

void buildDashboard()
{
    lv_obj_t *screen = lv_screen_active();
    lv_obj_set_style_bg_color(screen, color(0x050B08), 0);
    lv_obj_set_style_bg_opa(screen, LV_OPA_COVER, 0);
    lv_obj_clear_flag(screen, LV_OBJ_FLAG_SCROLLABLE);

    lv_obj_t *header = lv_obj_create(screen);
    lv_obj_set_pos(header, 0, 0);
    lv_obj_set_size(header, 800, 70);
    stylePanel(header, 0x07100B, 0x12331E, 0);

    lv_obj_t *logo = lv_obj_create(header);
    lv_obj_set_pos(logo, 20, 13);
    lv_obj_set_size(logo, 44, 44);
    stylePanel(logo, 0x0B3C22, 0x19E778, 10);
    lv_obj_t *logoText = makeText(logo, "K", 0, 0, &lv_font_montserrat_24, 0x19E778);
    lv_obj_center(logoText);

    makeText(header, "KERYX DISPLAY", 78, 10, &lv_font_montserrat_24, 0xF1FFF5);
    makeText(header, "WAVESHARE 4.3  |  WIFI TELEMETRY", 80, 41, &lv_font_montserrat_14, 0x63A57B);

    statusPill = lv_obj_create(header);
    lv_obj_set_pos(statusPill, 626, 17);
    lv_obj_set_size(statusPill, 154, 36);
    stylePanel(statusPill, 0x111A15, 0x2B3930, 18);
    statusDot = lv_obj_create(statusPill);
    lv_obj_set_pos(statusDot, 13, 13);
    lv_obj_set_size(statusDot, 10, 10);
    stylePanel(statusDot, 0x77827A, 0x77827A, 5);
    statusLabel = makeText(statusPill, "DEMARRAGE", 32, 9, &lv_font_montserrat_14, 0xC6D2CA);

    lv_obj_t *hashCard = makeCard(screen, 20, 90, 500, 180, "HASHRATE TOTAL");
    hashValueLabel = makeText(hashCard, "--", 18, 54, &lv_font_montserrat_48, 0xF3FFF7);
    hashUnitLabel = makeText(hashCard, "En attente de Keryx Manager", 20, 119, &lv_font_montserrat_18, 0x63A57B);

    lv_obj_t *tempCard = makeCard(screen, 540, 90, 240, 80, "TEMPERATURE MAX.");
    temperatureValueLabel = makeText(tempCard, "-- °C", 16, 39, &lv_font_montserrat_24, 0xF3FFF7);

    lv_obj_t *gpuCard = makeCard(screen, 540, 190, 240, 80, "GPU ACTIFS");
    gpuValueLabel = makeText(gpuCard, "--", 16, 39, &lv_font_montserrat_24, 0xF3FFF7);

    lv_obj_t *powerCard = makeCard(screen, 20, 290, 240, 110, "PUISSANCE");
    powerValueLabel = makeText(powerCard, "-- W", 16, 49, &lv_font_montserrat_24, 0xF3FFF7);

    lv_obj_t *sharesCard = makeCard(screen, 280, 290, 240, 110, "ACCEPTEES / REJETEES");
    sharesValueLabel = makeText(sharesCard, "-- / --", 16, 49, &lv_font_montserrat_24, 0x19E778);

    lv_obj_t *uptimeCard = makeCard(screen, 540, 290, 240, 110, "TEMPS DE MINAGE");
    uptimeValueLabel = makeText(uptimeCard, "--:--:--", 16, 49, &lv_font_montserrat_24, 0xF3FFF7);

    footerLabel = makeText(screen, "Initialisation de l'ecran...", 20, 437, &lv_font_montserrat_14, 0x6F8D79);
}

void setStatus(UiState state, const char *overrideText = nullptr)
{
    uint32_t accent = 0x77827A;
    uint32_t background = 0x111A15;
    const char *text = "DEMARRAGE";

    switch (state) {
    case UiState::Booting:
        break;
    case UiState::WifiSetup:
        accent = 0xFF9F1C;
        background = 0x2A1B09;
        text = "CONFIG WIFI";
        break;
    case UiState::Waiting:
        accent = 0xFFB020;
        background = 0x281E0A;
        text = "EN ATTENTE";
        break;
    case UiState::Mining:
        accent = 0x19E778;
        background = 0x0A2A18;
        text = "MINAGE";
        break;
    case UiState::Stopped:
        accent = 0x8B9890;
        background = 0x111A15;
        text = "ARRETE";
        break;
    case UiState::Warning:
        accent = 0xFF9F1C;
        background = 0x2A1B09;
        text = "ALERTE";
        break;
    case UiState::Offline:
        accent = 0xFF4D5A;
        background = 0x2B0D11;
        text = "HORS LIGNE";
        break;
    }

    if (overrideText != nullptr) {
        text = overrideText;
    }

    lv_obj_set_style_bg_color(statusPill, color(background), 0);
    lv_obj_set_style_border_color(statusPill, color(accent), 0);
    lv_obj_set_style_bg_color(statusDot, color(accent), 0);
    lv_obj_set_style_border_color(statusDot, color(accent), 0);
    lv_obj_set_style_text_color(statusLabel, color(accent), 0);
    lv_label_set_text(statusLabel, text);
}

void updateFooter(const char *message)
{
    if (WiFi.status() == WL_CONNECTED) {
        lv_label_set_text_fmt(footerLabel, "Wi-Fi %s  |  %s  |  %s", WiFi.SSID().c_str(), WiFi.localIP().toString().c_str(), message);
    } else {
        lv_label_set_text(footerLabel, message);
    }
}

void showWifiSetup()
{
    if (esp_lv_adapter_lock(500) != ESP_OK) {
        return;
    }
    setStatus(UiState::WifiSetup);
    lv_label_set_text(hashValueLabel, "CONFIGURATION WI-FI");
    lv_obj_set_style_text_font(hashValueLabel, &lv_font_montserrat_32, 0);
    lv_label_set_text_fmt(hashUnitLabel, "Reseau : %s\nMot de passe : %s\nPuis ouvrez 192.168.4.1", portalSsid.c_str(), kPortalPassword);
    lv_obj_set_width(hashUnitLabel, 455);
    lv_label_set_text(temperatureValueLabel, "-- °C");
    lv_label_set_text(powerValueLabel, "-- W");
    lv_label_set_text(gpuValueLabel, "--");
    lv_label_set_text(sharesValueLabel, "-- / --");
    lv_label_set_text(uptimeValueLabel, "--:--:--");
    lv_label_set_text(footerLabel, "Mode configuration locale - aucune donnee de minage transmise");
    esp_lv_adapter_unlock();
}

void showWaitingForManager()
{
    if (esp_lv_adapter_lock(500) != ESP_OK) {
        return;
    }
    setStatus(UiState::Waiting);
    lv_label_set_text(hashValueLabel, "--");
    lv_obj_set_style_text_font(hashValueLabel, &lv_font_montserrat_48, 0);
    lv_label_set_text(hashUnitLabel, "En attente de Keryx Manager");
    lv_obj_set_width(hashUnitLabel, LV_SIZE_CONTENT);
    updateFooter("Telemetrie locale UDP active");
    esp_lv_adapter_unlock();
}

void showOffline()
{
    if (esp_lv_adapter_lock(100) != ESP_OK) {
        return;
    }
    setStatus(UiState::Offline);
    lv_label_set_text(hashUnitLabel, "Keryx Manager ne transmet plus");
    updateFooter("Derniere telemetrie recue il y a plus de 10 s");
    esp_lv_adapter_unlock();
}

void formatHashRate(double hashesPerSecond, char *value, size_t valueSize, char *unit, size_t unitSize)
{
    double displayed = hashesPerSecond;
    const char *suffix = "H/s";
    if (hashesPerSecond >= 1'000'000'000.0) {
        displayed /= 1'000'000'000.0;
        suffix = "GH/s";
    } else if (hashesPerSecond >= 1'000'000.0) {
        displayed /= 1'000'000.0;
        suffix = "MH/s";
    } else if (hashesPerSecond >= 1'000.0) {
        displayed /= 1'000.0;
        suffix = "kH/s";
    }

    const int decimals = displayed >= 100.0 ? 1 : 2;
    snprintf(value, valueSize, "%.*f", decimals, displayed);
    for (char *cursor = value; *cursor != '\0'; ++cursor) {
        if (*cursor == '.') {
            *cursor = ',';
        }
    }
    snprintf(unit, unitSize, "%s", suffix);
}

void formatDuration(uint64_t seconds, char *output, size_t outputSize)
{
    const uint64_t days = seconds / 86'400;
    const uint32_t hours = static_cast<uint32_t>((seconds % 86'400) / 3'600);
    const uint32_t minutes = static_cast<uint32_t>((seconds % 3'600) / 60);
    const uint32_t remainingSeconds = static_cast<uint32_t>(seconds % 60);
    if (days > 0) {
        snprintf(output, outputSize, "%lluj %02u:%02u", static_cast<unsigned long long>(days), hours, minutes);
    } else {
        snprintf(output, outputSize, "%02u:%02u:%02u", hours, minutes, remainingSeconds);
    }
}

UiState mapTelemetryState(const Telemetry &telemetry)
{
    if (strcmp(telemetry.state, "mining") == 0) {
        return telemetry.hashRateHs <= 0.0 ? UiState::Warning : UiState::Mining;
    }
    if (strcmp(telemetry.state, "warning") == 0 || strcmp(telemetry.state, "error") == 0) {
        return UiState::Warning;
    }
    if (strcmp(telemetry.state, "stopped") == 0) {
        return UiState::Stopped;
    }
    return UiState::Waiting;
}

void renderTelemetry(const Telemetry &telemetry)
{
    char hashValue[24];
    char hashUnit[12];
    char duration[24];
    formatHashRate(telemetry.hashRateHs, hashValue, sizeof(hashValue), hashUnit, sizeof(hashUnit));
    formatDuration(telemetry.uptimeSeconds, duration, sizeof(duration));

    if (esp_lv_adapter_lock(250) != ESP_OK) {
        return;
    }
    const UiState state = mapTelemetryState(telemetry);
    setStatus(state, (state == UiState::Warning && telemetry.hashRateHs <= 0.0) ? "0 H/S" : nullptr);
    lv_obj_set_style_text_font(hashValueLabel, &lv_font_montserrat_48, 0);
    lv_label_set_text(hashValueLabel, hashValue);
    lv_label_set_text_fmt(hashUnitLabel, "%s  |  %s", hashUnit, telemetry.miner);
    lv_obj_set_width(hashUnitLabel, LV_SIZE_CONTENT);
    if (telemetry.maxTemperatureC > 0.0F) {
        lv_label_set_text_fmt(temperatureValueLabel, "%.0f °C", telemetry.maxTemperatureC);
    } else {
        lv_label_set_text(temperatureValueLabel, "-- °C");
    }
    if (telemetry.totalPowerW > 0.0F) {
        lv_label_set_text_fmt(powerValueLabel, "%.1f W", telemetry.totalPowerW);
    } else {
        lv_label_set_text(powerValueLabel, "-- W");
    }
    lv_label_set_text_fmt(gpuValueLabel, "%u GPU", telemetry.gpuCount);
    lv_label_set_text_fmt(sharesValueLabel, "%llu / %llu", static_cast<unsigned long long>(telemetry.accepted), static_cast<unsigned long long>(telemetry.rejected));
    lv_obj_set_style_text_color(sharesValueLabel, color(telemetry.rejected > 0 ? 0xFF9F1C : 0x19E778), 0);
    lv_label_set_text(uptimeValueLabel, duration);
    updateFooter("Keryx Manager connecte");
    esp_lv_adapter_unlock();
}

bool parseUnsigned(const char *text, uint64_t &value)
{
    if (text == nullptr || *text == '\0') {
        return false;
    }
    char *end = nullptr;
    const unsigned long long parsed = strtoull(text, &end, 10);
    if (end == text || *end != '\0') {
        return false;
    }
    value = static_cast<uint64_t>(parsed);
    return true;
}

bool parseFloat(const char *text, double &value)
{
    if (text == nullptr || *text == '\0') {
        return false;
    }
    char *end = nullptr;
    const double parsed = strtod(text, &end);
    if (end == text || *end != '\0' || !isfinite(parsed)) {
        return false;
    }
    value = parsed;
    return true;
}

bool parseTelemetry(char *packet, Telemetry &telemetry)
{
    constexpr size_t kExpectedFields = 11;
    char *fields[kExpectedFields] = {};
    char *save = nullptr;
    size_t count = 0;
    for (char *token = strtok_r(packet, "|", &save); token != nullptr && count < kExpectedFields; token = strtok_r(nullptr, "|", &save)) {
        fields[count++] = token;
    }
    if (count != kExpectedFields || strcmp(fields[0], "KX1") != 0) {
        return false;
    }

    uint64_t sequence = 0;
    uint64_t gpuCount = 0;
    uint64_t accepted = 0;
    uint64_t rejected = 0;
    uint64_t uptime = 0;
    double hashRate = 0.0;
    double temperature = 0.0;
    double power = 0.0;
    if (!parseUnsigned(fields[1], sequence) || !parseUnsigned(fields[3], gpuCount) ||
        !parseFloat(fields[4], hashRate) || !parseFloat(fields[5], temperature) ||
        !parseFloat(fields[6], power) || !parseUnsigned(fields[7], accepted) ||
        !parseUnsigned(fields[8], rejected) || !parseUnsigned(fields[9], uptime)) {
        return false;
    }
    if (gpuCount > 32 || hashRate < 0.0 || temperature < 0.0 || temperature > 150.0 || power < 0.0 || power > 100'000.0) {
        return false;
    }

    telemetry.sequence = static_cast<uint32_t>(sequence);
    telemetry.gpuCount = static_cast<uint8_t>(gpuCount);
    telemetry.hashRateHs = hashRate;
    telemetry.maxTemperatureC = static_cast<float>(temperature);
    telemetry.totalPowerW = static_cast<float>(power);
    telemetry.accepted = accepted;
    telemetry.rejected = rejected;
    telemetry.uptimeSeconds = uptime;
    strlcpy(telemetry.state, fields[2], sizeof(telemetry.state));
    strlcpy(telemetry.miner, fields[10], sizeof(telemetry.miner));
    return true;
}

String makeDeviceSuffix()
{
    const uint64_t mac = ESP.getEfuseMac();
    char suffix[7];
    snprintf(suffix, sizeof(suffix), "%06llX", static_cast<unsigned long long>(mac & 0xFFFFFF));
    return String(suffix);
}

void redirectToPortal()
{
    webServer.sendHeader("Location", String("http://") + WiFi.softAPIP().toString(), true);
    webServer.send(302, "text/plain", "");
}

void startPortal()
{
    udp.stop();
    WiFi.disconnect(true, false);
    delay(100);
    WiFi.mode(WIFI_AP);
    portalSsid = String("KERYX-DISPLAY-") + makeDeviceSuffix();
    WiFi.softAP(portalSsid.c_str(), kPortalPassword);
    dnsServer.start(53, "*", WiFi.softAPIP());

    webServer.on("/", HTTP_GET, []() {
        webServer.send_P(200, "text/html; charset=utf-8", kPortalHtml);
    });
    webServer.on("/save", HTTP_POST, []() {
        const String ssid = webServer.arg("ssid");
        const String password = webServer.arg("password");
        if (ssid.isEmpty() || ssid.length() > 32 || password.length() > 64) {
            webServer.send(400, "text/plain; charset=utf-8", "Parametres Wi-Fi invalides");
            return;
        }
        preferences.begin("keryx_wifi", false);
        preferences.putString("ssid", ssid);
        preferences.putString("password", password);
        preferences.end();
        webServer.send(200, "text/html; charset=utf-8", "<html><body style='background:#050b08;color:white;font-family:Segoe UI;padding:30px'><h2>Wi-Fi enregistre</h2><p>L'ecran redemarre...</p></body></html>");
        restartPending = true;
        restartAtMs = millis() + 1'500;
    });
    webServer.on("/generate_204", HTTP_GET, redirectToPortal);
    webServer.on("/hotspot-detect.html", HTTP_GET, redirectToPortal);
    webServer.onNotFound(redirectToPortal);
    webServer.begin();
    portalActive = true;
    showWifiSetup();
}

bool connectWifi()
{
    preferences.begin("keryx_wifi", true);
    const String ssid = preferences.getString("ssid", "");
    const String password = preferences.getString("password", "");
    preferences.end();
    if (ssid.isEmpty()) {
        return false;
    }

    if (esp_lv_adapter_lock(250) == ESP_OK) {
        setStatus(UiState::Waiting, "CONNEXION WIFI");
        lv_label_set_text_fmt(hashUnitLabel, "Connexion a %s...", ssid.c_str());
        esp_lv_adapter_unlock();
    }

    WiFi.mode(WIFI_STA);
    WiFi.setAutoReconnect(true);
    WiFi.persistent(false);
    WiFi.begin(ssid.c_str(), password.c_str());
    const uint32_t startedAt = millis();
    while (WiFi.status() != WL_CONNECTED && millis() - startedAt < kWifiConnectTimeoutMs) {
        delay(100);
    }
    return WiFi.status() == WL_CONNECTED;
}

void beginStationServices()
{
    hostName = String("keryx-display-") + makeDeviceSuffix();
    hostName.toLowerCase();
    MDNS.begin(hostName.c_str());
    const bool udpReady = udp.begin(kTelemetryPort);
    Serial.printf("UDP: listen %s:%u ready=%d\n", WiFi.localIP().toString().c_str(), kTelemetryPort, udpReady ? 1 : 0);
    disconnectedSinceMs = 0;
    lastWifiRetryMs = millis();
    telemetryIsStale = true;
    showWaitingForManager();
}

void processTelemetry()
{
    const int packetSize = udp.parsePacket();
    if (packetSize <= 0) {
        return;
    }
    udpPacketCount++;
    Serial.printf("UDP: packet #%u from %s:%u size=%d\n", udpPacketCount,
        udp.remoteIP().toString().c_str(), udp.remotePort(), packetSize);
    if (packetSize >= 512) {
        udpInvalidPacketCount++;
        while (udp.available()) {
            udp.read();
        }
        Serial.println("UDP: rejected oversized packet");
        return;
    }

    char packet[512];
    const int read = udp.read(packet, sizeof(packet) - 1);
    if (read <= 0) {
        udpInvalidPacketCount++;
        Serial.println("UDP: packet read failed");
        return;
    }
    packet[read] = '\0';
    Telemetry telemetry;
    if (!parseTelemetry(packet, telemetry)) {
        udpInvalidPacketCount++;
        Serial.printf("UDP: rejected payload=%s\n", packet);
        return;
    }

    lastTelemetryMs = millis();
    telemetryIsStale = false;
    renderTelemetry(telemetry);
    udpRenderedPacketCount++;
    Serial.printf("UDP: rendered sequence=%u state=%s gpu=%u hash=%.3f\n",
        telemetry.sequence, telemetry.state, telemetry.gpuCount, telemetry.hashRateHs);
}

void processWifiHealth()
{
    const uint32_t now = millis();
    if (WiFi.status() == WL_CONNECTED) {
        if (disconnectedSinceMs != 0) {
            disconnectedSinceMs = 0;
            udp.stop();
            udp.begin(kTelemetryPort);
            showWaitingForManager();
        }
        return;
    }

    if (disconnectedSinceMs == 0) {
        disconnectedSinceMs = now;
    }
    if (now - lastWifiRetryMs >= kWifiRetryEveryMs) {
        lastWifiRetryMs = now;
        WiFi.reconnect();
    }
    if (now - disconnectedSinceMs >= kPortalAfterOfflineMs) {
        startPortal();
    }
}

void initDisplay()
{
    Serial.println("Display: creating board");
    Board *board = new Board();
    if (board == nullptr || !board->init()) {
        Serial.println("Board init failed");
        while (true) {
            delay(1'000);
        }
    }
    Serial.printf("Display: board initialized, PSRAM=%u bytes, free=%u bytes\n",
        static_cast<unsigned>(ESP.getPsramSize()), static_cast<unsigned>(ESP.getFreePsram()));

    const auto rotation = ESP_LV_ADAPTER_ROTATE_0;
    const auto tearMode = ESP_LV_ADAPTER_TEAR_AVOID_MODE_DEFAULT_RGB;
    const uint8_t frameBufferCount = esp_lv_adapter_get_required_frame_buffer_count(tearMode, rotation);
    LCD *lcd = board->getLCD();
    if (lcd == nullptr) {
        Serial.println("LCD unavailable");
        while (true) {
            delay(1'000);
        }
    }
    auto *lcdBus = lcd->getBus();
    if (lcdBus->getBasicAttributes().type == ESP_PANEL_BUS_TYPE_RGB) {
        lcd->configFrameBufferNumber(frameBufferCount);
        static_cast<BusRGB *>(lcdBus)->configRGB_BounceBufferSize(lcd->getFrameWidth() * 10);
    }
    if (!board->begin()) {
        Serial.println("Board begin failed");
        while (true) {
            delay(1'000);
        }
    }
    Serial.println("Display: panel and backlight started");

    if constexpr (kPanelColorBarDiagnostic) {
        const bool colorBarOk = lcd->colorBarTest();
        Serial.printf("Display: persistent color-bar diagnostic=%s\n", colorBarOk ? "OK" : "FAILED");
        while (true) {
            delay(1'000);
        }
    }

    esp_lv_adapter_config_t adapterConfig = ESP_LV_ADAPTER_DEFAULT_CONFIG();
    adapterConfig.task_stack_size = 12 * 1024;
    adapterConfig.task_priority = 2;
    adapterConfig.task_core_id = ARDUINO_RUNNING_CORE;
    ESP_ERROR_CHECK(esp_lv_adapter_init(&adapterConfig));

    esp_lv_adapter_display_config_t displayConfig = ESP_LV_ADAPTER_DISPLAY_RGB_DEFAULT_CONFIG(
        lcd,
        static_cast<uint16_t>(lcd->getFrameWidth()),
        static_cast<uint16_t>(lcd->getFrameHeight()),
        rotation
    );
    displayConfig.profile.use_psram = true;
    lv_display_t *display = esp_lv_adapter_register_display(&displayConfig);
    if (display == nullptr) {
        Serial.println("LVGL display registration failed");
        while (true) {
            delay(1'000);
        }
    }
    ESP_ERROR_CHECK(esp_lv_adapter_start());
    Serial.println("Display: LVGL adapter started");

    ESP_ERROR_CHECK(esp_lv_adapter_lock(-1));
    buildDashboard();
    setStatus(UiState::Booting);
    esp_lv_adapter_unlock();
    Serial.println("Display: dashboard ready");
}

} // namespace

void setup()
{
    Serial.begin(115200);
    delay(1'500);
    Serial.println("Keryx Display 0.8.1 booting");
    initDisplay();

    if (connectWifi()) {
        Serial.printf("Wi-Fi connected: %s, IP=%s\n", WiFi.SSID().c_str(), WiFi.localIP().toString().c_str());
        beginStationServices();
    } else {
        Serial.println("Wi-Fi profile unavailable, starting local setup portal");
        startPortal();
    }
}

void loop()
{
    static uint32_t lastDiagnosticMs = 0;
    const uint32_t now = millis();
    if (now - lastDiagnosticMs >= 5'000) {
        lastDiagnosticMs = now;
        Serial.printf("Alive: Wi-Fi=%d ip=%s portal=%d udp=%u invalid=%u rendered=%u free_heap=%u free_psram=%u\n",
            static_cast<int>(WiFi.status()), WiFi.localIP().toString().c_str(), portalActive ? 1 : 0,
            udpPacketCount, udpInvalidPacketCount, udpRenderedPacketCount,
            static_cast<unsigned>(ESP.getFreeHeap()), static_cast<unsigned>(ESP.getFreePsram()));
    }

    if (portalActive) {
        dnsServer.processNextRequest();
        webServer.handleClient();
        if (restartPending && static_cast<int32_t>(millis() - restartAtMs) >= 0) {
            ESP.restart();
        }
        delay(2);
        return;
    }

    processWifiHealth();
    if (!portalActive && WiFi.status() == WL_CONNECTED) {
        processTelemetry();
        if (!telemetryIsStale && millis() - lastTelemetryMs > kTelemetryTimeoutMs) {
            telemetryIsStale = true;
            showOffline();
        }
    }
    delay(2);
}
