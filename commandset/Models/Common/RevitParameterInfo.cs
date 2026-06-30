namespace RevitMCPCommandSet.Models.Common
{
    public class RevitParameterInfo
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string StorageType { get; set; }
        public bool IsReadOnly { get; set; }
        public string Group { get; set; }
    }
}
