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
        IList<string> filesWithErrors = _textDocHandler.CreateTranslation(request.IsCSV);
        return Task.FromResult<NotificationResponse>(new() { Data = filesWithErrors });
    }
}

[Method("dialog/generateTranslation")]
public class NotificationRequest : IRequest<NotificationResponse>
{
    public bool IsCSV { get; set; }
}


public class NotificationResponse
{
    public IList<string> Data { get; set; } = [];
}