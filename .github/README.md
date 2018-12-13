# Anemonis.AspNetCore.JsonRpc

[JSON-RPC 2.0](http://www.jsonrpc.org/specification) middleware for ASP.NET Core 2 based on the [JSON-RPC 2.0 Transport: HTTP](https://www.simple-is-better.org/json-rpc/transport_http.html) specification and the [Anemonis.JsonRpc](https://github.com/alexanderkozlenko/json-rpc)
 serializer.

[![NuGet package](https://img.shields.io/nuget/v/Anemonis.AspNetCore.JsonRpc.svg?style=flat-square)](https://www.nuget.org/packages/Anemonis.AspNetCore.JsonRpc)
[![Gitter](https://img.shields.io/gitter/room/nwjs/nw.js.svg?style=flat-square)](https://gitter.im/anemonis/aspnetcore-json-rpc)

## Project Details

- The middleware transparently handles batch JSON-RPC requests.
- The middleware automatically handles common JSON-RPC issues.
- The middleware does not verify the `Content-Length` header.
- A service supports default method parameter values for named parameters not provided in a request.

In addition to the standard JSON-RPC error codes the middleware may return the following JSON-RPC errors:

| Code | Reason |
| :---: | --- |
| `-32000` | The provided batch contains requests with duplicate identifiers |

In addition to the standard JSON-RPC HTTP error codes the middleware may return the following HTTP error codes:

 Code | Reason |
 :---: | --- |
 `415` | The `Content-Encoding` header is specified |

With logger factory availability, the following events may appear in a journal:

 ID | Level | Reason |
 :---: | --- | --- |
 `1000` | Debug | A JSON-RPC request accepted for processing as a single item |
 `1010` | Debug | A JSON-RPC request accepted for processing as a batch |
 `2000` | Information | A JSON-RPC request processed as notification |
 `2010` | Information | A JSON-RPC request processed with result |
 `2020` | Information | A JSON-RPC request processed with error |
 `2030` | Information | A JSON-RPC request processed with result as notification due to client  demand |
 `2040` | Information | A JSON-RPC request processed with error as notification due to client  demand |
 `3000` | Warning | A JSON-RPC request processed as notification due to server configuration |
 `4000` | Error | An error occurred during deserialization of a JSON-RPC request |
 `4010` | Error | A JSON-RPC request is not considered as a valid JSON-RPC message |
 `4020` | Error | A JSON-RPC batch contains requests with duplicate identifiers |

## Code Examples

```cs
public class JsonRpcService : IJsonRpcService
{
    [JsonRpcMethod("m1", "p1", "p2")]
    public Task<long> InvokeMethod1Async(long p1, long p2)
    {
        if (p2 == 0L)
        {
            throw new JsonRpcServiceException(100L);
        }

        return Task.FromResult(p1 / p2);
    }

    [JsonRpcMethod("m2", 0, 1)]
    public Task<long> InvokeMethod2Async(long p1, long p2)
    {
        return Task.FromResult(p1 + p2);
    }
}

public class Startup : IStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddJsonRpcService<JsonRpcService>();
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseJsonRpcService<JsonRpcService>("/api");
    }
}
```
or
```cs
public class JsonRpcHandler : IJsonRpcHandler
{
    public IReadOnlyDictionary<string, JsonRpcRequestContract> GetContracts()
    {
        var contract1Types = new Dictionary<string, Type>();
        var contract2Types = new Type[2];

        contract1Types["p1"] = typeof(long);
        contract1Types["p2"] = typeof(long);
        contract2Types[0] = typeof(long);
        contract2Types[1] = typeof(long);

        var contracts = new Dictionary<string, JsonRpcRequestContract>();

        contracts["m1"] = new JsonRpcRequestContract(contract1Types);
        contracts["m2"] = new JsonRpcRequestContract(contract2Types);

        return contracts;
    }

    public Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request)
    {
        var response = default(JsonRpcResponse);

        switch (request.Method)
        {
            case "m1":
                {
                    var p1 = (long)request.ParametersByName["p1"];
                    var p2 = (long)request.ParametersByName["p2"];

                    response = p2 != 0L ?
                        new JsonRpcResponse(p1 / p2, request.Id) :
                        new JsonRpcResponse(new JsonRpcError(100L), request.Id);
                }
                break;
            case "m2":
                {
                    var p1 = (long)request.ParametersByPosition[0];
                    var p2 = (long)request.ParametersByPosition[1];

                    response = new JsonRpcResponse(p1 + p2, request.Id);
                }
                break;
        }

        return Task.FromResult(response);
    }
}

public class Startup : IStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddJsonRpcHandler<JsonRpcHandler>();
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseJsonRpcHandler<JsonRpcHandler>("/api");
    }
}
```

## Quicklinks

- [Contributing Guidelines](./CONTRIBUTING.md)
- [Code of Conduct](./CODE_OF_CONDUCT.md)