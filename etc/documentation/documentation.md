### Sample of using JSON-RPC middleware

1. Define types for request parameters and result objects:

```cs
class CalculatorOperands
{
    [JsonProperty("operand_1")]
    public double Operand1 { get; set; }

    [JsonProperty("operand_2")]
    public double Operand2 { get; set; }
}
```

2. Implement RPC-JSON handler interface:

```cs
class JsonRpcCalculatorHandler : IJsonRpcHandler
{
    public JsonRpcSerializerScheme CreateScheme()
    {
        var result = new JsonRpcSerializerScheme();

        result.Methods["plus"] = new JsonRpcMethodScheme(typeof(CalculatorOperands));

        return result;
    }

    public Task HandleNotification(JsonRpcRequest request)
    {
        throw new InvalidOperationException("Notifications are not supported");
    }

    public Task<JsonRpcResponse> HandleRequest(JsonRpcRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var operands = (CalculatorOperands)request.Params;
        var response = default(JsonRpcResponse);

        switch (request.Method)
        {
            case "plus":
                {
                    response = new JsonRpcResponse(operands.Operand1 + operands.Operand2, request.Id);
                }
                break;
            default:
                {
                    throw new InvalidOperationException($"Unsupported operation: \"{request.Method}\"");
                }
        }

        return Task.FromResult(response);
    }
}
```

3. Register implemented handler in web host builder:

```cs
builder.Configure(app => app.UseJsonRpc("/calculator", new JsonRpcCalculatorHandler()))
```