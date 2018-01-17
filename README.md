## Community.AspNetCore.JsonRpc

[JSON-RPC 2.0](http://www.jsonrpc.org/specification) middleware for ASP.NET Core based on [JSON-RPC 2.0 Transport: HTTP](https://www.simple-is-better.org/json-rpc/transport_http.html) specification.

[![NuGet package](https://img.shields.io/nuget/v/Community.AspNetCore.JsonRpc.svg?style=flat-square)](https://www.nuget.org/packages/Community.AspNetCore.JsonRpc)

```cs
public class MyJsonRpcHandler : IJsonRpcHandler
{
    public IReadOnlyDictionary<string, JsonRpcRequestContract> CreateScheme()
    {
        return new Dictionary<string, JsonRpcRequestContract>
        {
            ["nam"] = new JsonRpcRequestContract(
                new Dictionary<string, Type>
                {
                    ["pr1"] = typeof(long),
                    ["pr2"] = typeof(long)
                }),
            ["pos"] = new JsonRpcRequestContract(
                new[]
                {
                    typeof(long),
                    typeof(long)
                }),
            ["err"] = JsonRpcRequestContract.Default,
            ["not"] = JsonRpcRequestContract.Default
        };
    }

    public Task<JsonRpcResponse> Handle(JsonRpcRequest request)
    {
        var response = default(JsonRpcResponse);

        switch (request.Method)
        {
            case "nam":
                {
                    var parameter1 = (long)request.ParamsByName["pr1"];
                    var parameter2 = (long)request.ParamsByName["pr2"];

                    response = new JsonRpcResponse(parameter1 - parameter2, request.Id);
                }
                break;
            case "pos":
                {
                    var parameter1 = (long)request.ParamsByPosition[0];
                    var parameter2 = (long)request.ParamsByPosition[1];

                    response = new JsonRpcResponse(parameter1 + parameter2, request.Id);
                }
                break;
            case "err":
                {
                    var error = new JsonRpcError(100L, "94cccbe7-d613-4aca-8940-9298892b8ee6");

                    response = new JsonRpcResponse(error, request.Id);
                }
                break;
            case "not":
                {
                }
                break;
        }

        return Task.FromResult(response);
    }
}
```
```cs
builder.Configure(_ => _.UseJsonRpcHandler<MyJsonRpcHandler>());
```
or
```cs
public class MyJsonRpcService
{
    [JsonRpcName("nam")]
    public Task<long> MethodWithParamsByName([JsonRpcName("pr1")] long parameter1, [JsonRpcName("pr2")] long parameter2)
    {
        return Task.FromResult(parameter1 - parameter2);
    }

    [JsonRpcName("pos")]
    public Task<long> MethodWithParamsByPosition(long parameter1, long parameter2)
    {
        return Task.FromResult(parameter1 + parameter2);
    }

    [JsonRpcName("err")]
    public Task<long> MethodWithErrorResponse()
    {
        throw new JsonRpcServiceException(100L, "94cccbe7-d613-4aca-8940-9298892b8ee6");
    }

    [JsonRpcName("not")]
    public Task MethodWithNotification()
    {
        return Task.CompletedTask;
    }
}
```
```cs
builder.Configure(_ => _.UseJsonRpcService<MyJsonRpcService>());
```

- `JsonRpcName` attribute can be used on an interface as well.
- Parameters provided by name can utilize default parameter value if the particular parameter is not provided by the client.