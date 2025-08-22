using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.JsonRpc;

namespace GameDialog.Server;

public class NotificationHandler : IJsonRpcRequestHandler<NotificationRequest, NotificationResponse>
{
    public NotificationHandler(TextDocumentHandler textDocumentHandler)
    {
        _textDocHandler = textDocumentHandler;
    }

    private readonly TextDocumentHandler _textDocHandler;

    public Task<NotificationResponse> Handle(NotificationRequest request, CancellationToken cancellationToken)
    {
        List<string> filesWithErrors = _textDocHandler.CompileAndGenerateAllFiles();
        return Task.FromResult<NotificationResponse>(new() { Data = filesWithErrors });
    }
}

[Method("dialog/recompileAllFiles")]
public class NotificationRequest : IRequest<NotificationResponse>
{
}

public class NotificationResponse
{
    public List<string> Data { get; set; } = [];
}