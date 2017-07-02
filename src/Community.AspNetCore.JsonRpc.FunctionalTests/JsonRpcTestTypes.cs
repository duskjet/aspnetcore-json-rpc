using Newtonsoft.Json;

namespace Community.AspNetCore.JsonRpc.FunctionalTests
{
    [JsonObject(MemberSerialization.OptIn)]
    internal struct CalculatorOperands
    {
        [JsonProperty("operand_1")]
        public double Operand1 { get; set; }

        [JsonProperty("operand_2")]
        public double Operand2 { get; set; }
    }
}