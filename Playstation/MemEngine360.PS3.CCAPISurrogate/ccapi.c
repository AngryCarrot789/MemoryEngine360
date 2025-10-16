#include "ccapi.h"
#include <stdio.h>
#include <stdlib.h>
#include <windows.h>

#define LITTLE_ENDIAN

static HMODULE LibHandle = NULL;
static int LibLoaded = 0;
static uint32_t ProcessId = ~0;
static int (__cdecl*NATIVE_CCAPIConnectConsole)(const char* ip) = NULL;
static int (__cdecl*NATIVE_CCAPIDisconnectConsole)() = NULL;
static int (__cdecl*NATIVE_CCAPIGetConnectionStatus)(int* status) = NULL;
static int (__cdecl*NATIVE_CCAPISetBootConsoleIds)(ConsoleIdType idType, int on, const ConsoleId* id) = NULL;
static int (__cdecl*NATIVE_CCAPISetConsoleIds)(ConsoleIdType idType, const ConsoleId* id) = NULL;
static int (__cdecl*NATIVE_CCAPISetMemory)(uint32_t pid, uint64_t address, uint32_t size, const void* data) = NULL;
static int (__cdecl*NATIVE_CCAPIGetMemory)(uint32_t pid, uint64_t address, uint32_t size, void* data) = NULL;
static int (__cdecl*NATIVE_CCAPIGetProcessList)(uint32_t* npid, uint32_t* pids) = NULL;
static int (__cdecl*NATIVE_CCAPIGetProcessName)(uint32_t pid, ProcessName* name) = NULL;
static int (__cdecl*NATIVE_CCAPIGetTemperature)(int* cell, int* rsx) = NULL;
static int (__cdecl*NATIVE_CCAPIShutdown)(ShutdownMode mode) = NULL;
static int (__cdecl*NATIVE_CCAPIRingBuzzer)(BuzzerType type) = NULL;
static int (__cdecl*NATIVE_CCAPISetConsoleLed)(ColorLed color, StatusLed status) = NULL;
static int (__cdecl*NATIVE_CCAPIGetFirmwareInfo)(uint32_t* firmware, uint32_t* ccapi, ConsoleType* cType) = NULL;
static int (__cdecl*NATIVE_CCAPIVshNotify)(NotifyIcon icon, const char* msg) = NULL;
static int (__cdecl*NATIVE_CCAPIGetNumberOfConsoles)() = NULL;
static void (__cdecl*NATIVE_CCAPIGetConsoleInfo)(int index, ConsoleName* name, ConsoleIp* ip) = NULL;
static int (__cdecl*NATIVE_CCAPIGetDllVersion)() = NULL;

inline static void reverse(register uint8_t* b, register uint8_t* e) {
    for (e--; e - b > 0; b++, e--) {
        const register uint8_t t = *b;
        *b = *e;
        *e = t;
    }
}

int ccapi_init(const char* libpath) {
    if (LibHandle)
        return CCAPI_OK;

    LibHandle = LoadLibraryA(libpath);

    if (!LibHandle)
        return CCAPI_ERROR;

    NATIVE_CCAPIConnectConsole      = (int  (__cdecl*) (const char*))  GetProcAddress(LibHandle, "CCAPIConnectConsole");
    NATIVE_CCAPIDisconnectConsole   = (int  (__cdecl*) ()) GetProcAddress(LibHandle, "CCAPIDisconnectConsole");
    NATIVE_CCAPIGetConnectionStatus = (int  (__cdecl*) (int*)) GetProcAddress(LibHandle, "CCAPIGetConnectionStatus");
    NATIVE_CCAPISetBootConsoleIds   = (int  (__cdecl*) (ConsoleIdType, int, const ConsoleId*)) GetProcAddress(LibHandle, "CCAPISetBootConsoleIds");
    NATIVE_CCAPISetConsoleIds       = (int  (__cdecl*) (ConsoleIdType, const ConsoleId*))  GetProcAddress(LibHandle, "CCAPISetConsoleIds");
    NATIVE_CCAPISetMemory           = (int  (__cdecl*) (uint32_t, uint64_t, uint32_t, const void*))GetProcAddress(LibHandle, "CCAPISetMemory");
    NATIVE_CCAPIGetMemory           = (int  (__cdecl*) (uint32_t, uint64_t, uint32_t, void*))  GetProcAddress(LibHandle, "CCAPIGetMemory");
    NATIVE_CCAPIGetProcessList      = (int  (__cdecl*) (uint32_t*, uint32_t*)) GetProcAddress(LibHandle, "CCAPIGetProcessList");
    NATIVE_CCAPIGetProcessName      = (int  (__cdecl*) (uint32_t, ProcessName*))   GetProcAddress(LibHandle, "CCAPIGetProcessName");
    NATIVE_CCAPIGetTemperature      = (int  (__cdecl*) (int* cell, int* rsx))  GetProcAddress(LibHandle, "CCAPIGetTemperature");
    NATIVE_CCAPIShutdown            = (int  (__cdecl*) (ShutdownMode)) GetProcAddress(LibHandle, "CCAPIShutdown");
    NATIVE_CCAPIRingBuzzer          = (int  (__cdecl*) (BuzzerType))   GetProcAddress(LibHandle, "CCAPIRingBuzzer");
    NATIVE_CCAPISetConsoleLed       = (int  (__cdecl*) (ColorLed, StatusLed))  GetProcAddress(LibHandle, "CCAPISetConsoleLed");
    NATIVE_CCAPIGetFirmwareInfo     = (int  (__cdecl*) (uint32_t*, uint32_t*, ConsoleType*))   GetProcAddress(LibHandle, "CCAPIGetFirmwareInfo");
    NATIVE_CCAPIVshNotify           = (int  (__cdecl*) (NotifyIcon, const char*))  GetProcAddress(LibHandle, "CCAPIVshNotify");
    NATIVE_CCAPIGetNumberOfConsoles = (int  (__cdecl*) ()) GetProcAddress(LibHandle, "CCAPIGetNumberOfConsoles");
    NATIVE_CCAPIGetConsoleInfo      = (void (__cdecl*) (int, ConsoleName*, ConsoleIp*))GetProcAddress(LibHandle, "CCAPIGetConsoleInfo");
    NATIVE_CCAPIGetDllVersion       = (int  (__cdecl*) ()) GetProcAddress(LibHandle, "CCAPIGetDllVersion");

    LibLoaded = NATIVE_CCAPIConnectConsole != NULL
        && NATIVE_CCAPIDisconnectConsole != NULL
        && NATIVE_CCAPIGetConnectionStatus != NULL
        && NATIVE_CCAPISetBootConsoleIds != NULL
        && NATIVE_CCAPISetConsoleIds != NULL
        && NATIVE_CCAPISetMemory != NULL
        && NATIVE_CCAPIGetMemory != NULL
        && NATIVE_CCAPIGetProcessList != NULL
        && NATIVE_CCAPIGetProcessName != NULL
        && NATIVE_CCAPIGetTemperature != NULL
        && NATIVE_CCAPIShutdown != NULL
        && NATIVE_CCAPIRingBuzzer != NULL
        && NATIVE_CCAPISetConsoleLed != NULL
        && NATIVE_CCAPIGetFirmwareInfo != NULL
        && NATIVE_CCAPIVshNotify != NULL
        && NATIVE_CCAPIGetNumberOfConsoles != NULL
        && NATIVE_CCAPIGetConsoleInfo != NULL
        && NATIVE_CCAPIGetDllVersion != NULL;

    return LibLoaded ? CCAPI_OK : CCAPI_ERROR;
}

int ccapi_free(void) {
    if (LibHandle != NULL) {
        FreeLibrary(LibHandle);
        LibHandle = NULL;

        NATIVE_CCAPIConnectConsole = NULL;
        NATIVE_CCAPIDisconnectConsole = NULL;
        NATIVE_CCAPIGetConnectionStatus = NULL;
        NATIVE_CCAPISetBootConsoleIds = NULL;
        NATIVE_CCAPISetConsoleIds = NULL;
        NATIVE_CCAPISetMemory = NULL;
        NATIVE_CCAPIGetMemory = NULL;
        NATIVE_CCAPIGetProcessList = NULL;
        NATIVE_CCAPIGetProcessName = NULL;
        NATIVE_CCAPIGetTemperature = NULL;
        NATIVE_CCAPIShutdown = NULL;
        NATIVE_CCAPIRingBuzzer = NULL;
        NATIVE_CCAPISetConsoleLed = NULL;
        NATIVE_CCAPIGetFirmwareInfo = NULL;
        NATIVE_CCAPIVshNotify = NULL;
        NATIVE_CCAPIGetNumberOfConsoles = NULL;
        NATIVE_CCAPIGetConsoleInfo = NULL;
        NATIVE_CCAPIGetDllVersion = NULL;
    }

    LibLoaded = 0;
    return CCAPI_OK;
}

int ccapi_get_library_state(void) {
    return LibLoaded;
}

int ccapi_connect(const char* ip) {
    return NATIVE_CCAPIConnectConsole(ip);
}

int ccapi_disconnect(void) {
    return NATIVE_CCAPIDisconnectConsole();
}

uint32_t ccapi_get_attached_process(void) {
    return ProcessId;
}

int ccapi_attach_process(const uint32_t pid) {
    ProcessId = pid;
    return CCAPI_OK;
}

int ccapi_find_game_process(uint32_t* p_found_pid, char** pp_process_name) {
    ProcessName name;
    uint32_t pid_array[32];
    uint32_t pid_count = sizeof(pid_array) / sizeof(pid_array[0]);
    int ret = ccapi_get_process_list(&pid_count, pid_array);
    if (ret != CCAPI_OK) {
        return ret;
    }

    for (uint32_t i = 0; i < pid_count; i++) {
        ret = ccapi_get_process_name(pid_array[i], &name);
        if (ret != CCAPI_OK) {
            return ret;
        }

        if (!strstr(name.value, "dev_flash")) {
            *p_found_pid = pid_array[i];
            if (pp_process_name) {
                const size_t cch_process_name = strlen(name.value);
                if (cch_process_name > 0) {
                    const size_t cb_name_buffer = cch_process_name + 1;
                    char* name_buffer = malloc(cb_name_buffer);
                    if (name_buffer != NULL) {
                        memcpy(name_buffer, name.value, cch_process_name);
                        name_buffer[cch_process_name] = '\0';
                        *pp_process_name = name_buffer;
                    }
                }
            }
            
            return CCAPI_OK;
        }
    }

    return CCAPI_OK;
}

int ccapi_set_boot_console_ids(const ConsoleIdType idType, const ConsoleId* id) {
    return NATIVE_CCAPISetBootConsoleIds(idType, 1, id);
}

int ccapi_set_boot_console_ids_string(const ConsoleIdType idType, const char* id) {
    ConsoleId cid;
    ccapi_string_to_array(id, cid.value);
    return ccapi_set_boot_console_ids(idType, &cid);
}

int ccapi_reset_boot_console_ids(const ConsoleIdType idType) {
    return NATIVE_CCAPISetBootConsoleIds(idType, 0,NULL);
}

int ccapi_set_console_ids(const ConsoleIdType idType, const ConsoleId* id) {
    return NATIVE_CCAPISetConsoleIds(idType, id);
}

int ccapi_set_console_ids_string(const ConsoleIdType idType, const char* id) {
    ConsoleId cid;
    ccapi_string_to_array(id, cid.value);
    return ccapi_set_console_ids(idType, &cid);
}

int ccapi_write_memory(const uint64_t address, const uint32_t size, const void* data) {
    return NATIVE_CCAPISetMemory(ProcessId, address, size, data);
}

int ccapi_write_memory_i8(const uint64_t address, const uint8_t data) {
    return ccapi_write_memory(address, sizeof(data), &data);
}

int ccapi_write_memory_i32(const uint64_t address, uint32_t data) {
#ifdef LITTLE_ENDIAN
    uint8_t* data8 = (uint8_t*)&data;
    reverse(data8, data8 + sizeof(data));
#endif
    return ccapi_write_memory(address, sizeof(data), &data);
}

int ccapi_write_memory_f32(const uint64_t address, float32_t data) {
#ifdef LITTLE_ENDIAN
    uint8_t* data8 = (uint8_t*)&data;
    reverse(data8, data8 + sizeof(data));
#endif
    return ccapi_write_memory(address, sizeof(data), &data);
}

int ccapi_write_memory_i64(const uint64_t address, uint64_t data) {
#ifdef LITTLE_ENDIAN
    uint8_t* data8 = (uint8_t*)&data;
    reverse(data8, data8 + sizeof(data));
#endif
    return ccapi_write_memory(address, sizeof(data), &data);
}

int ccapi_write_memory_f64(const uint64_t address, float64_t data) {
#ifdef LITTLE_ENDIAN
    uint8_t* data8 = (uint8_t*)&data;
    reverse(data8, data8 + sizeof(data));
#endif
    return ccapi_write_memory(address, sizeof(data), &data);
}

int ccapi_read_memory(const uint64_t address, const uint32_t size, void* data) {
    return NATIVE_CCAPIGetMemory(ProcessId, address, size, data);
}

uint8_t ccapi_read_memory_i8(const uint64_t address, int* ret) {
    uint8_t data;
    const int r = ccapi_read_memory(address, sizeof(data), &data);
    if (ret)
        *ret = r;
    return data;
}

uint32_t ccapi_read_memory_i32(const uint64_t address, int* ret) {
    uint32_t data;
    const int r = ccapi_read_memory(address, sizeof(data), &data);
    if (ret)
        *ret = r;
#ifdef LITTLE_ENDIAN
    uint8_t* data8 = (uint8_t*)&data;
    reverse(data8, data8 + sizeof(data));
#endif
    return data;
}

float32_t ccapi_read_memory_f32(const uint64_t address, int* ret) {
    float32_t data;
    const int r = ccapi_read_memory(address, sizeof(data), &data);
#ifdef LITTLE_ENDIAN
    uint8_t* data8 = (uint8_t*)&data;
    reverse(data8, data8 + sizeof(data));
#endif
    if (ret)
        *ret = r;
    return data;
}

uint64_t ccapi_read_memory_i64(const uint64_t address, int* ret) {
    uint64_t data;
    const int r = ccapi_read_memory(address, sizeof(data), &data);
#ifdef LITTLE_ENDIAN
    uint8_t* data8 = (uint8_t*)&data;
    reverse(data8, data8 + sizeof(data));
#endif
    if (ret)
        *ret = r;
    return data;
}

float64_t ccapi_read_memory_f64(const uint64_t address, int* ret) {
    float64_t data;
    const int r = ccapi_read_memory(address, sizeof(data), &data);
#ifdef LITTLE_ENDIAN
    uint8_t* data8 = (uint8_t*)&data;
    reverse(data8, data8 + sizeof(data));
#endif
    if (ret)
        *ret = r;
    return data;
}

int ccapi_get_process_list(uint32_t* npid, uint32_t* pids) {
    return NATIVE_CCAPIGetProcessList(npid, pids);
}

int ccapi_get_process_name(const uint32_t pid, ProcessName* name) {
    return NATIVE_CCAPIGetProcessName(pid, name);
}

int ccapi_get_temperature(int* cell, int* rsx) {
    return NATIVE_CCAPIGetTemperature(cell, rsx);
}

int ccapi_shutdown(const ShutdownMode mode) {
    return NATIVE_CCAPIShutdown(mode);
}

int ccapi_ring_buzzer(const BuzzerType type) {
    return NATIVE_CCAPIRingBuzzer(type);
}

int ccapi_set_console_led(const ColorLed color, const StatusLed status) {
    return NATIVE_CCAPISetConsoleLed(color, status);
}

int ccapi_get_version(uint32_t* version) {
    return NATIVE_CCAPIGetFirmwareInfo(NULL, version,NULL);
}

int ccapi_get_firmware(uint32_t* firmware) {
    return NATIVE_CCAPIGetFirmwareInfo(firmware,NULL,NULL);
}

int ccapi_get_console_type(ConsoleType* ctype) {
    return NATIVE_CCAPIGetFirmwareInfo(NULL,NULL, ctype);
}

char* ccapi_firmware_to_string(const uint32_t firmware, char* s, const int size) {
    const uint32_t h = (firmware >> 24);
    const uint32_t l = ((firmware >> 12) & 0xFF);
    snprintf(s, size, "%01x.%02x", h, l);
    return s;
}

const char* ccapi_console_type_to_string(const ConsoleType cType) {
    const char* s = "UNK";

    switch (cType) {
    case CEX:
        s = "CEX";
        break;

    case DEX:
        s = "DEX";
        break;

    case TOOL:
        s = "TOOL";
        break;

    default:
        break;
    }

    return s;
}

int ccapi_vsh_notify(const NotifyIcon icon, const char* msg) {
    return NATIVE_CCAPIVshNotify(icon, msg);
}

int ccapi_get_number_of_consoles(void) {
    return NATIVE_CCAPIGetNumberOfConsoles();
}

void ccapi_get_console_info(const int index, ConsoleName* name, ConsoleIp* ip) {
    NATIVE_CCAPIGetConsoleInfo(index, name, ip);
}

int ccapi_get_dll_version(void) {
    return NATIVE_CCAPIGetDllVersion();
}

int ccapi_write_string(const uint64_t address, const char* str) {
    return ccapi_write_memory(address, strlen(str) + 1, str);
}

int ccapi_read_string(const uint64_t address, char* str, const int size) {
    return ccapi_read_memory(address, size, str);
}

uint8_t* ccapi_string_to_array(const char* s, uint8_t* id) {
    const uint32_t len = strlen(s);
    if (!len) {
        return id;
    }

    int j = 0;
    for (uint32_t i = 0; i < (len + 1); i += 2) {
        char b[3] = {0, 0, 0};
        strncpy_s(b, 3, &s[i], 2);
        b[1] = b[1] ? b[1] : '0';
        id[j++] = strtoul(b,NULL, 16);
    }
    return id;
}
