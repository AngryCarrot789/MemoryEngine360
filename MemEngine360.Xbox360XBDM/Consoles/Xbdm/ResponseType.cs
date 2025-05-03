// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemEngine360.
// 
// MemEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

namespace MemEngine360.Xbox360XBDM.Consoles.Xbdm;

public enum ResponseType : int {
    SingleResponse = 200, 
    Connected = 201,
    MultiResponse = 202, //terminates with period
    BinaryResponse = 203,
    ReadyForBinary = 204,
    NowNotifySession = 205, // notificaiton channel/ dedicated connection (XBDM_DEDICATED)
    UndefinedError = 400, 
    MaxConnectionsExceeded = 401,
    FileNotFound = 402,
    NoSuchModule = 403,
    MemoryNotMapped = 404, //setzerobytes or setmem failed
    NoSuchThread = 405,
    ClockNotSet = 406, //linetoolong or clocknotset
    UnknownCommand = 407,
    NotStopped = 408,
    FileMustBeCopied = 409,
    FileAlreadyExists = 410,
    DirectoryNotEmpty = 411,
    BadFileName = 412,
    FileCannotBeCreated = 413,
    AccessDenied = 414,
    NoRoomOnDevice = 415,
    NotDebuggable = 416,
    TypeInvalid = 417,
    DataNotAvailable = 418,
    BoxIsNotLocked = 420,
    KeyExchangeRequired = 421,
    DedicatedConnectionRequired = 422,
    InvalidArgument = 423,
    ProfileNotStarted = 424,
    ProfileAlreadyStarted = 425,
    D3DDebugCommandNotImplemented = 480,
    D3DInvalidSurface = 481,
    VxTaskPending = 496,
    VxTooManySessions = 497,
}