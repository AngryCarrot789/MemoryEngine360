namespace MemEngine360.Connections.Impl;

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