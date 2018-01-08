## Community.AspNetCore.JsonRpc

Provides [JSON-RPC 2.0](http://www.jsonrpc.org/specification) support for ASP.NET Core based on [JSON-RPC 2.0 Transport: HTTP](https://www.simple-is-better.org/json-rpc/transport_http.html) proposal.

[![NuGet package](https://img.shields.io/nuget/v/Community.AspNetCore.JsonRpc.svg?style=flat-square)](https://www.nuget.org/packages/Community.AspNetCore.JsonRpc)

```cs
class CalculatorHandler : IJsonRpcHandler
{
    public JsonRpcSerializerScheme CreateScheme()
    {
        var scheme = new JsonRpcSerializerScheme();

        scheme.Methods["pin"] = new JsonRpcMethodScheme();
        scheme.Methods["clr"] = new JsonRpcMethodScheme();
        scheme.Methods["add"] = new JsonRpcMethodScheme(
            new[]
            {
                typeof(long),
                typeof(long)
            });
        scheme.Methods["sub"] = new JsonRpcMethodScheme(
            new Dictionary<string, Type>
            {
                ["o1"] = typeof(long),
                ["o2"] = typeof(long)
            });

        return scheme;
    }

    public Task<JsonRpcResponse> Handle(JsonRpcRequest request)
    {
        var response = default(JsonRpcResponse);

        switch (request.Method)
        {
            case "clr":
                {
                    var error = new JsonRpcError(100L, "OPERATION_NOT_AVAILABLE");
                    
                    response = new JsonRpcResponse(error, request.Id);
                }
                break;
            case "add":
                {
                    var operand1 = (long)request.ParamsByPosition[0];
                    var operand2 = (long)request.ParamsByPosition[1];
                    
                    response = new JsonRpcResponse(operand1 + operand2, request.Id);
                }
                break;
            case "sub":
                {
                    var operand1 = (long)request.ParamsByName["o1"];
                    var operand2 = (long)request.ParamsByName["o2"];
                    
                    response = new JsonRpcResponse(operand1 - operand2, request.Id);
                }
                break;
        }

        return Task.FromResult(response);
    }
}
```
```cs
builder.Configure(app => app.UseJsonRpcHandler<CalculatorHandler>());
```
or
```cs
class CalculatorService
{
    [JsonRpcName("pin")]
    public Task Ping()
    {
        return Task.CompletedTask;
    }

    [JsonRpcName("clr")]
    public Task Clear()
    {
        throw new JsonRpcServiceException(100L, "OPERATION_NOT_AVAILABLE");
    }

    [JsonRpcName("add")]
    public Task<long> Add(long operand1, long operand2)
    {
        return Task.FromResult(operand1 + operand2);
    }

    [JsonRpcName("sub")]
    public Task<long> Substract([JsonRpcName("o1")]long operand1, [JsonRpcName("o2")]long operand2)
    {
        return Task.FromResult(operand1 - operand2);
    }
}
```
```cs
builder.Configure(app => app.UseJsonRpcService<CalculatorService>());
```