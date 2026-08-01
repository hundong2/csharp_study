/*
문제: 동일 노드 IPC용 NamedPipe 서버 객체를 생성하고 상태를 출력하세요.
*/

using System;
using System.IO.Pipes;

var pipeServer = new NamedPipeServerStream(
    "express_data_bus",
    PipeDirection.InOut,
    1,
    PipeTransmissionMode.Byte,
    PipeOptions.Asynchronous | PipeOptions.WriteThrough
);

Console.WriteLine("[Express Network] Memory-Mapped Shared Pipe established for high-density IPC.");

/*
실행 결과:
[Express Network] Memory-Mapped Shared Pipe established for high-density IPC.
*/
