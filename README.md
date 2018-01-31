## Community.AspNetCore.JsonRpc

[JSON-RPC 2.0](http://www.jsonrpc.org/specification) middleware for ASP.NET Core 2.0 based on [JSON-RPC 2.0 Transport: HTTP](https://www.simple-is-better.org/json-rpc/transport_http.html) specification.

[![NuGet package](https://img.shields.io/nuget/v/Community.AspNetCore.JsonRpc.svg?style=flat-square)](https://www.nuget.org/packages/Community.AspNetCore.JsonRpc)

### Features

- Middleware transparently handles single and batch JSON-RPC requests.
- Middleware automatically handles and sends the corresponding JSON-RPC responses for common issues (e.g. invalid JSON, invalid JSON-RPC message structure, invalid JSON-RPC contract, etc.).
- A handler / service can be acquired from a service provider or instantiated directly for a request scope.
- A handler / service which implements `IDisposable` interface will be automatically disposed on request scope exit.
- The `JsonRpcName` attribute can be used on an interface for a service handler as well.
- Parameters provided by name can utilize default parameter value if the particular parameter is not provided by the client.

If a logger factory is available in the service provider, the following entries can appear in a journal with the all related details (method names and request identifiers):

ID | Category | Purpose
--- | --- | ---
1000 | Error | An error occurred during deserialization of a JSON-RPC request
1010 | Error | A request is not considered as a valid JSON-RPC request
2000 | Warning | A JSON-RPC request processed as notification due to server configuration
2010 | Warning | A JSON-RPC request processed as notification due to client configuration
3000 | Information | A JSON-RPC request processed successfully
3010 | Information | A JSON-RPC request processed with error
3020 | Information | A JSON-RPC request processed as notification
4010 | Trace | A JSON-RPC request with a single item accepted for processing
4020 | Trace | A JSON-RPC request with multiple items accepted for processing

```cs
public class MyJsonRpcService
{
    [JsonRpcName("nam")]
    public Task<long> MethodWithParamsByName(
        [JsonRpcName("pr1")] long parameter1,
        [JsonRpcName("pr2")] long parameter2)
    {
        return Task.FromResult(parameter1 - parameter2);
    }

    [JsonRpcName("pos")]
    public Task<long> MethodWithParamsByPosition(
        long parameter1,
        long parameter2)
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
builder
    .ConfigureServices(sc => sc.AddJsonRpcService<MyJsonRpcService>())
    .Configure(ab => ab.UseJsonRpcService<MyJsonRpcService>("/api"))
```
or
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

    public Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request)
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
builder
    .ConfigureServices(sc => sc.AddJsonRpcHandler<MyJsonRpcHandler>())
    .Configure(ab => ab.UseJsonRpcHandler<MyJsonRpcHandler>("/api"))
```