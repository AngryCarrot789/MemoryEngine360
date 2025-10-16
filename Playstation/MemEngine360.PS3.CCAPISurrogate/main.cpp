// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemoryEngine360.
// 
// MemoryEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemoryEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemoryEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

#include <cstdio>
#include <cstdint>
#include <winsock2.h>
#include <Ws2tcpip.h>
#include "ccapi.h"

#pragma comment(lib, "ws2_32.lib")


static_assert(sizeof(uint8_t) == 1, "sizeof(uint8_t) != 1");
static_assert(sizeof(int8_t) == 1, "sizeof(int8_t) != 1");
static_assert(sizeof(uint16_t) == 2, "sizeof(uint16_t) != 2");
static_assert(sizeof(int16_t) == 2, "sizeof(int16_t) != 2");
static_assert(sizeof(uint32_t) == 4, "sizeof(uint32_t) != 4");
static_assert(sizeof(int32_t) == 4, "sizeof(int32_t) != 4");
static_assert(sizeof(uint64_t) == 8, "sizeof(uint64_t) != 8");
static_assert(sizeof(int64_t) == 8, "sizeof(int64_t) != 8");

static int run_network_loop(char*, SOCKET);
static int process_packet(SOCKET, int32_t, const char*, int32_t);

static int recv_exact_from_network(const SOCKET s, char* buffer, const int32_t count) {
    int cb_received = 0;
    while (cb_received < count) {
        const int n = recv(s, buffer + cb_received, count - cb_received, 0);
        if (n <= 0)
            return -1;

        cb_received += n;
    }

    return cb_received;
}

static int send_exact(const SOCKET s, const char* buffer, const int32_t count) {
    int cb_sent = 0;
    while (cb_sent < count) {
        const int n = send(s, buffer + cb_sent, count - cb_sent, 0);
        if (n <= 0)
            return -1;

        cb_sent += n;
    }

    return cb_sent;
}

static int send_uint8(const SOCKET client, uint8_t value) {
    return send_exact(client, reinterpret_cast<char*>(&value), sizeof(uint8_t));
}

static int send_int16(const SOCKET client, int16_t value) {
    return send_exact(client, reinterpret_cast<char*>(&value), sizeof(int16_t));
}

static int send_uint16(const SOCKET client, uint16_t value) {
    return send_exact(client, reinterpret_cast<char*>(&value), sizeof(uint16_t));
}

static int send_int32(const SOCKET client, int32_t value) {
    return send_exact(client, reinterpret_cast<char*>(&value), sizeof(int32_t));
}

static int send_uint32(const SOCKET client, uint32_t value) {
    return send_exact(client, reinterpret_cast<char*>(&value), sizeof(uint32_t));
}

static int send_int64(const SOCKET client, int64_t value) {
    return send_exact(client, reinterpret_cast<char*>(&value), sizeof(int64_t));
}

static int send_uint64(const SOCKET client, uint64_t value) {
    return send_exact(client, reinterpret_cast<char*>(&value), sizeof(uint64_t));
}

static int send_buffer_with_tag(const SOCKET client, const char* data, const int32_t count) {
    int ret;
    if ((ret = send_int32(client, count)) < 0)
        return ret;
    if ((ret = send_exact(client, data, count)) < 0)
        return ret;
    return count + sizeof(int32_t);
}

static uint8_t recv_uint8_from_buffer(const char* recv_buffer) {
    return reinterpret_cast<const uint8_t*>(recv_buffer)[0];
}

static int16_t recv_int16_from_buffer(const char* recv_buffer) {
    return reinterpret_cast<const int16_t*>(recv_buffer)[0];
}

static uint16_t recv_uint16_from_buffer(const char* recv_buffer) {
    return reinterpret_cast<const uint16_t*>(recv_buffer)[0];
}

static int32_t recv_int32_from_buffer(const char* recv_buffer) {
    return reinterpret_cast<const int32_t*>(recv_buffer)[0];
}

static uint32_t recv_uint32_from_buffer(const char* recv_buffer) {
    return reinterpret_cast<const uint32_t*>(recv_buffer)[0];
}

static int64_t recv_int64_from_buffer(const char* recv_buffer) {
    return reinterpret_cast<const int64_t*>(recv_buffer)[0];
}

static uint64_t recv_uint64_from_buffer(const char* recv_buffer) {
    return reinterpret_cast<const uint64_t*>(recv_buffer)[0];
}

static int recv_u16tagged_into_new_buffer(const char* recv_buffer, char** pp_dst_buffer, uint16_t* p_cch_dst_buffer, const BOOL string) {
    uint16_t cch_dst_buffer = recv_uint16_from_buffer(recv_buffer);
    *p_cch_dst_buffer = cch_dst_buffer;

    // append an extra byte for null-terminated strings
    const int32_t cb_alloc = cch_dst_buffer + (string ? 1 : 0);
    void* data = malloc(cb_alloc);
    if (data == NULL) {
        return -1; // out of memory?
    }

    memcpy(data, recv_buffer + sizeof(uint16_t), cch_dst_buffer);
    *pp_dst_buffer = static_cast<char*>(data);
    if (string) {
        *((char*)data + cch_dst_buffer) = NULL; // set null char
    }

    return 0;
}

// A helper to send a string instead of using send_buffer_with_tag
static int send_string_with_tag(const SOCKET client, const char* text) {
    const size_t cch_text = strlen(text);
    if (cch_text > INT_MAX) {
        return -2;
    }

    const int32_t cch_text_int = static_cast<int32_t>(cch_text);

    int ret;
    if ((ret = send_int32(client, cch_text_int)) < 0)
        return ret;
    if ((ret = send_exact(client, text, cch_text_int)) < 0)
        return ret;

    return 0;
}

int main(const int argc, char* argv[]) {
    if (argc < 2) {
        printf("Port argument required\n");
        return 1;
    }

    const int port = atoi(argv[1]);
    if (port <= 0 || port > 65535) {
        printf("Invalid port: %s\n", argv[1]);
        return 1;
    }

    WSADATA wsa_data;
    int result = WSAStartup(MAKEWORD(2, 2), &wsa_data);
    if (result != 0) {
        printf("WSAStartup failed: %d\n", result);
        return 1;
    }

    const SOCKET s = socket(AF_INET, SOCK_STREAM, 0);
    if (s == INVALID_SOCKET) {
        printf("Error at socket(): %d\n", WSAGetLastError());
        return 1;
    }

    sockaddr_in addr = {0};
    addr.sin_family = AF_INET;
    InetPton(AF_INET, L"127.0.0.1", &addr.sin_addr.s_addr);
    addr.sin_port = htons(static_cast<uint16_t>(port));

    result = bind(s, reinterpret_cast<sockaddr*>(&addr), sizeof(addr));
    if (result != 0) {
        printf("Failed to bind to ANY on port 34567\n");
        return 1;
    }

    printf("Listening on port %d\n", port);
    listen(s, 1);

    const SOCKET client = accept(s, NULL, NULL);

    // Max buffer = 64K
    void* buffer = malloc(0x10000);
    const int loop_result = run_network_loop(static_cast<char*>(buffer), client);
    free(buffer);

    printf("Loop exit (%d)\n", loop_result);
    closesocket(client);
    closesocket(s);
    WSACleanup();
    ccapi_free();
    return 0;
}

int run_network_loop(char* buffer, const SOCKET client) {
    while (true) {
        if (recv_exact_from_network(client, buffer, 8) < 0) {
            return 0; // connection closed
        }

        const int32_t* header = reinterpret_cast<int32_t*>(buffer);
        const int32_t cmd_id = header[0];
        if (cmd_id < 0) {
            printf("Received invalid command id: %d", cmd_id);
            return -1;
        }

        const int32_t cb_data = header[1];
        if (cb_data < 0 || cb_data > 0x10000) {
            printf("Received invalid data buffer size: %d", cb_data);
            return -1;
        }

        printf("Received packet. Id = %d, cb_data = %d\n", cmd_id, cb_data);

        if (cb_data > 0) {
            // we overwrite the in-buffer header with packet data, 
            // since we have the header as local vars now
            if (recv_exact_from_network(client, buffer, cb_data) < 0) {
                return 0; // connection closed
            }
        }

        printf("Processing packet %d with %d bytes of packet data\n", cmd_id, cb_data);
        const int result = process_packet(client, cmd_id, buffer, cb_data);
        if (result != 0) {
            return result;
        }
    }
}

/*
RPC functions to implement
int CCAPIConnectConsole      (const char* ip)
int CCAPIDisconnectConsole   ()
int CCAPIGetConnectionStatus (int* status)
int CCAPISetBootConsoleIds   (ConsoleIdType idType, int on, const ConsoleId* id)
int CCAPISetConsoleIds       (ConsoleIdType idType, const ConsoleId* id)
int CCAPISetMemory           (uint32_t pid, uint64_t address, uint32_t size, const void* data)
int CCAPIGetMemory           (uint32_t pid, uint64_t address, uint32_t size, void* data)
int CCAPIGetProcessList      (uint32_t* npid, uint32_t* pids)
int CCAPIGetProcessName      (uint32_t pid, ProcessName* name)
int CCAPIGetTemperature      (int* cell, int* rsx)
int CCAPIShutdown            (ShutdownMode mode)
int CCAPIRingBuzzer          (BuzzerType type)
int CCAPISetConsoleLed       (ColorLed color, StatusLed status)
int CCAPIGetFirmwareInfo     (uint32_t* firmware, uint32_t* ccapi, ConsoleType* cType)
int CCAPIVshNotify           (NotifyIcon icon, const char* msg)
int CCAPIGetNumberOfConsoles ()
void CCAPIGetConsoleInfo     (int index, ConsoleName* name, ConsoleIp* ip)
int CCAPIGetDllVersion       ()
 */

#define CHECK_RESULT(m) \
if ((ret = (m)) < 0)  \
    return ret

static int handle_get_memory(const SOCKET client, const char* recv_buffer, const int32_t cb_recv_buffer) {
    int ret;
    if (cb_recv_buffer != 8) {
        printf("Invalid args to CCAPIGetMemory. Expected 8 bytes");
        return -1;
    }

    constexpr uint32_t chunk_size = 0x7FFF; // 32767

    const uint64_t address = recv_uint32_from_buffer(recv_buffer);
    uint32_t cb_remaining = recv_uint32_from_buffer(recv_buffer + sizeof(uint32_t));

    void* read_buffer = malloc(min(cb_remaining, chunk_size));
    if (read_buffer == 0) {
        printf("Failed to alloc temporary buffer to GetMemory");
        return -1;
    }

    CHECK_RESULT(send_uint8(client, 1 /* one return value, the array of bytes w/o length prefix */));

    while (cb_remaining > 0) {
        const int32_t cb_send = min(cb_remaining, chunk_size);
        if ((ret = ccapi_read_memory(address, cb_send, read_buffer)) < 0) {
            send_uint16(client, 0x8000);
            free(read_buffer);
            return 0;
        }

        const uint16_t header = static_cast<uint16_t>(cb_send);
        if ((ret = send_uint16(client, header)) < 0) {
            free(read_buffer);
            return ret;
        }

        if ((ret = send_exact(client, static_cast<const char*>(read_buffer), cb_send)) < 0) {
            free(read_buffer);
            return ret;
        }

        cb_remaining -= cb_send;
    }

    free(read_buffer);
    return 0;
}

static int handle_set_memory(const SOCKET client, const char* recv_buffer, const int32_t cb_recv_buffer) {
    int ret;
    if (cb_recv_buffer < sizeof(uint32_t)) {
        printf("Invalid args to CCAPISetMemory. Expected >= 4 bytes");
        return -1;
    }

    const uint32_t address = recv_uint32_from_buffer(recv_buffer);
    const uint32_t cb_to_write = cb_recv_buffer - sizeof(uint32_t);
    const char* to_write = recv_buffer + sizeof(uint32_t);
    CHECK_RESULT(send_uint8(client, 1));
    CHECK_RESULT(send_int32(client, ccapi_write_memory(address, cb_to_write, to_write)));
    return 0;
}

int handle_setup(const SOCKET client) {
    int ret;

    const int setup_ret = ccapi_init("CCAPI.dll");
    CHECK_RESULT(send_uint8(client, 1));
    CHECK_RESULT(send_uint8(client, setup_ret == 0 ? 1 : 0));

    return 0;
}

int process_packet(const SOCKET client, const int32_t cmd_id, const char* recv_buffer, const int32_t cb_recv_buffer) {
    int ret = 0;

    switch (cmd_id) {
    case 1:
        printf("Command Run - ccapi_init (CCAPI.dll)\n");
        if ((ret = handle_setup(client)) != 0) {
            return ret;
        }

        break;

    case 2:
        printf("Command Run - ccapi_free\n");
        ccapi_free();
        CHECK_RESULT(send_uint8(client, 0)); // void response
        return -1; // return non-zero to exit network loop

    case 3:
        printf("Command Run - self test functionality\n");
        if (cb_recv_buffer > 0) {
            CHECK_RESULT(send_uint8(client, 3)); // 3 vars

            char* text;
            uint16_t cch_text;
            CHECK_RESULT(recv_u16tagged_into_new_buffer(recv_buffer, &text, &cch_text, true /* string */));
            CHECK_RESULT(send_buffer_with_tag(client, text, cch_text));
        }
        else {
            CHECK_RESULT(send_uint8(client, 2)); // 2 vars
        }

        CHECK_RESULT(send_string_with_tag(client, "This is param 1!!!"));
        CHECK_RESULT(send_int32(client, 1234567));
        break;

    case 4:
        printf("Command Run - ccapi_connect\n");
        if (cb_recv_buffer < 2) {
            printf("Invalid args to CCAPIConnectConsole. Expected >= 2 bytes");
            return -1;
        }

        char* ip_address;
        uint16_t cch_ip_address;
        CHECK_RESULT(recv_u16tagged_into_new_buffer(recv_buffer, &ip_address, &cch_ip_address, true /* string */));
        CHECK_RESULT(send_uint8(client, 1));
        CHECK_RESULT(send_int32(client, ccapi_connect(ip_address)));
        free(ip_address);

        break;

    case 5:
        printf("Command Run - ccapi_disconnect\n");
        CHECK_RESULT(send_uint8(client, 1));
        CHECK_RESULT(send_int32(client, ccapi_disconnect()));
        break;

    case 9:
        printf("Command Run - ccapi_set_memory\n");
        if ((ret = handle_set_memory(client, recv_buffer, cb_recv_buffer) != 0)) {
            return ret;
        }

        break;

    case 10:
        printf("Command Run - ccapi_read_memory\n");
        if ((ret = handle_get_memory(client, recv_buffer, cb_recv_buffer) != 0)) {
            return ret;
        }

        break;

    case 22:
        printf("Command Run - ccapi_attach_process\n");
        if (cb_recv_buffer < 4) {
            printf("Invalid args to ccapi_attach_process. Expected 4 bytes");
            return -1;
        }

        {
            const uint32_t old_pid = ccapi_get_attached_process();
            const uint32_t pid = recv_uint32_from_buffer(recv_buffer);
            ccapi_attach_process(pid);

            CHECK_RESULT(send_uint8(client, 1));
            CHECK_RESULT(send_uint32(client, old_pid));
        }

        break;
    case 23:
        printf("Command Run - ccapi_find_game_process\n");
        {
            uint32_t found_pid = 0;
            char* name_buffer;
            const int ccapi_ret = ccapi_find_game_process(&found_pid, &name_buffer);

            CHECK_RESULT(send_uint8(client, name_buffer != NULL ? 2 : 1));
            CHECK_RESULT(send_uint32(client, ccapi_ret == 0 ? found_pid : 0));
            if (name_buffer != NULL) {
                send_string_with_tag(client, name_buffer);
            }
        }

        break;

    case 24:
        printf("Command Run - ccapi_get_process_list\n");
        {
            ProcessName name;
            uint32_t pid_array[32];
            uint32_t pid_count = sizeof(pid_array) / sizeof(pid_array[0]);
            if ((ret = ccapi_get_process_list(&pid_count, pid_array)) != CCAPI_OK) {
                return -1;
            }

            CHECK_RESULT(send_uint8(client, static_cast<uint8_t>(pid_count)));
            for (uint32_t i = 0; i < pid_count; i++) {
                if ((ret = ccapi_get_process_name(pid_array[i], &name)) != CCAPI_OK) {
                    return -1;
                }
                
                CHECK_RESULT(send_uint32(client, pid_array[i]));
                CHECK_RESULT(send_string_with_tag(client, name.value));
            }
        }

        break;

    default:
        printf("Received invalid command id: %d\n", cmd_id);
        return -1; // return non-zero to exit network loop
    }

    return 0;
}
