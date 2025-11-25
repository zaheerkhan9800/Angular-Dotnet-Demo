namespace BackendDotnet.Models
{
    public class CalcRequest
    {
        public double A { get; set; }
        public double B { get; set; }
        public string Op { get; set; } = string.Empty;
    }
}
