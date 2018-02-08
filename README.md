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

### Specifics

In addition to the standard JSON-RPC error codes the middleware may return the following JSON-RPC errors:

Code | Reason
:---: | ---
`-32000` | The provided batch contains requests with duplicate identifiers

In addition to the JSON-RPC HTTP transport specification the middleware may return the following HTTP status codes:

Code | Reason
:---: | ---
`400` | The query string is not empty
`400` | The `Content-Encoding` header is specified
`400` | The `Content-Length` header contains invalid value

If a logger factory is available in the service provider, the following events will appear in a journal with the all related details (method names and request identifiers):

ID | Level | Reason
:---: | --- | ---
`1000` | Error | An error occurred during deserialization of a JSON-RPC request
`1010` | Error | A JSON-RPC request is not considered as a valid JSON-RPC message
`2000` | Warning | A JSON-RPC request processed as notification due to server configuration
`2010` | Warning | A JSON-RPC request processed with result as notification due to client demand
`2020` | Warning | A JSON-RPC request processed with error as notification due to client demand
`3000` | Information | A JSON-RPC request processed as notification
`3010` | Information | A JSON-RPC request processed with result
`3020` | Information | A JSON-RPC request processed with error
`4000` | Trace | A JSON-RPC request accepted for processing as a single item
`4010` | Trace | A JSON-RPC request accepted for processing as a batch

### Samples

```cs
public class MyJsonRpcService : IJsonRpcService
{
    [JsonRpcName("m1")]
    public Task<long> Method1(
        [JsonRpcName("p1")] long parameter1,
        [JsonRpcName("p2")] long parameter2)
    {
        if (parameter2 == 0L)
        {
            throw new JsonRpcServiceException(100L);
        }

        return Task.FromResult(parameter1 / parameter2);
    }

    [JsonRpcName("m2")]
    public Task<long> Method2(
        long parameter1,
        long parameter2)
    {
        return Task.FromResult(parameter1 + parameter2);
    }
}
```
\+
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
            ["m1"] = new JsonRpcRequestContract(
                new Dictionary<string, Type>
                {
                    ["p1"] = typeof(long),
                    ["p2"] = typeof(long)
                }),
            ["m2"] = new JsonRpcRequestContract(
                new[]
                {
                    typeof(long),
                    typeof(long)
                })
        };
    }

    public Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request)
    {
        var response = default(JsonRpcResponse);

        switch (request.Method)
        {
            case "m1":
                {
                    var parameter1 = (long)request.ParamsByName["p1"];
                    var parameter2 = (long)request.ParamsByName["p2"];

                    response = parameter2 != 0L ?
                        new JsonRpcResponse(parameter1 / parameter2, request.Id) :
                        new JsonRpcResponse(new JsonRpcError(100L), request.Id);
                }
                break;
            case "m2":
                {
                    var parameter1 = (long)request.ParamsByPosition[0];
                    var parameter2 = (long)request.ParamsByPosition[1];

                    response = new JsonRpcResponse(parameter1 + parameter2, request.Id);
               }
                break;
        }

        return Task.FromResult(response);
    }
}
```
\+
```cs
builder
    .ConfigureServices(sc => sc.AddJsonRpcHandler<MyJsonRpcHandler>())
    .Configure(ab => ab.UseJsonRpcHandler<MyJsonRpcHandler>("/api"))
```