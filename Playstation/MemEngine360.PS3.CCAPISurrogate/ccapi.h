/**
*
* CCAPI C Wrapper made by Enstone
* Compatible with CCAPI 2.60, CCAPI 2.70, CCAPI 2.80, +
* Requires CCAPI.dll
* V1.00
*
**/

#ifndef CCAPI_H
#define CCAPI_H

#include <stdint.h>
#include "ccapi_types.h"

#define LITTLE_ENDIAN
#define CCAPI_OK 0
#define CCAPI_ERROR (-1)

//structs
struct ConsoleId {
    uint8_t value[16];
};

struct ProcessName {
    char value[512];
};

struct ConsoleName {
    char value[256];
};

struct ConsoleIp {
    char value[256];
};

//enums
enum ConsoleIdType {
    Idps = 0,
    Psid = 1,
};

enum ShutdownMode {
    ActionShutdown = 1,
    ActionSoftReboot = 2,
    ActionHardReboot = 3,
};

enum BuzzerType {
    BuzzerContinious = 0,
    BuzzerSingle = 1,
    BuzzerDouble = 2,
    BuzzerTriple = 3,
};

enum ColorLed {
    LedGreen = 1,
    LedRed = 2,
};

enum StatusLed {
    LedOff = 0,
    LedOn = 1,
    LedBlink = 2,
};

enum NotifyIcon {
    NotifyInfo = 0,
    NotifyCaution = 1,
    NotifyFriend = 2,
    NotifySlider = 3,
    NotifyWrongWay = 4,
    NotifyDialog = 5,
    NotifyDalogShadow = 6,
    NotifyText = 7,
    NotifyPointer = 8,
    NotifyGrab = 9,
    NotifyHand = 10,
    NotifyPen = 11,
    NotifyFinger = 12,
    NotifyArrow = 13,
    NotifyArrowRight = 14,
    NotifyProgress = 15,
    NotifyTrophy1 = 16,
    NotifyTrophy2 = 17,
    NotifyTrophy3 = 18,
    NotifyTrophy4 = 19
};

enum ConsoleType {
    UNK = 0,
    CEX = 1,
    DEX = 2,
    TOOL = 3,
};

typedef struct ConsoleId ConsoleId;
typedef struct ProcessName ProcessName;
typedef struct ConsoleName ConsoleName;
typedef struct ConsoleIp ConsoleIp;

//enums
typedef enum ConsoleIdType ConsoleIdType;
typedef enum ShutdownMode ShutdownMode;
typedef enum BuzzerType BuzzerType;
typedef enum ColorLed ColorLed;
typedef enum StatusLed StatusLed;
typedef enum NotifyIcon NotifyIcon;
typedef enum ConsoleType ConsoleType;

#ifdef __cplusplus
extern "C" {
#endif
/**functions**/
int ccapi_init(const char* path);
int ccapi_free();
int ccapi_get_library_state();
uint32_t ccapi_get_attached_process();
int ccapi_attach_process(uint32_t pid);
int ccapi_find_game_process(uint32_t* p_found_pid);
int ccapi_connect(const char* ip);
int ccapi_disconnect();
int ccapi_set_boot_console_ids(ConsoleIdType idType, const ConsoleId* id);
int ccapi_set_boot_console_ids_string(ConsoleIdType idType, const char* id);
int ccapi_reset_boot_console_ids(ConsoleIdType idType);
int ccapi_set_console_ids(ConsoleIdType idType, const ConsoleId* id);
int ccapi_set_console_ids_string(ConsoleIdType idType, const char* id);
int ccapi_write_memory(uint64_t address, uint32_t size, const void* data);
int ccapi_write_memory_i8(uint64_t address, uint8_t data);
int ccapi_write_memory_i32(uint64_t address, uint32_t data);
int ccapi_write_memory_f32(uint64_t address, float32_t data);
int ccapi_write_memory_i64(uint64_t address, uint64_t data);
int ccapi_write_memory_f64(uint64_t address, float64_t data);
int ccapi_read_memory(uint64_t address, uint32_t size, void* data);
uint8_t ccapi_read_memory_i8(uint64_t address, int* ret);
uint32_t ccapi_read_memory_i32(uint64_t address, int* ret);
float32_t ccapi_read_memory_f32(uint64_t address, int* ret);
uint64_t ccapi_read_memory_i64(uint64_t address, int* ret);
float64_t ccapi_read_memory_f64(uint64_t address, int* ret);
int ccapi_get_process_list(uint32_t* npid, uint32_t* pids);
int ccapi_get_process_name(uint32_t pid, ProcessName* name);
int ccapi_get_temperature(int* cell, int* rsx);
int ccapi_shutdown(ShutdownMode mode);
int ccapi_ring_buzzer(BuzzerType type);
int ccapi_set_console_led(ColorLed color, StatusLed status);
int ccapi_get_version(uint32_t* version);
int ccapi_get_firmware(uint32_t* firmware);
int ccapi_get_console_type(ConsoleType* ctype);
int ccapi_vsh_notify(NotifyIcon icon, const char* msg);
int ccapi_get_number_of_consoles();
void ccapi_get_console_info(int index, ConsoleName* name, ConsoleIp* ip);
int ccapi_get_dll_version();
char* ccapi_firmware_to_string(uint32_t firmware, char* s, int size);
const char* ccapi_console_type_to_string(ConsoleType cType);
int ccapi_write_string(uint64_t address, const char* str);
int ccapi_read_string(uint64_t address, char* str, int size);
uint8_t* ccapi_string_to_array(const char* s, uint8_t* id);
#ifdef __cplusplus
}
#endif
#endif
