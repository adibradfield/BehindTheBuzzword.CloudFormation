namespace BehindTheBuzzword.CloudFormation.DTO
{
    public class CustomResourceRequest
    {
        public string StackId { get; set; }
        public string ResponseURL { get; set; }
        public string RequestType { get; set; }
        public string ResourceType { get; set; }
        public string RequestId { get; set; }
        public string LogicalResourceId { get; set; }
        public string PhysicalResourceId { get; set; }
    }

    public class CustomResourceRequest<T> : CustomResourceRequest
    {
        public T ResourceProperties { get; set; }
        public T OldResourceProperties { get; set; }
    }

    class CustomResourceResponse
    {
        public string Status { get; set; }
        public string Reason { get; set; }
        public string PhysicalResourceId { get; set; }
        public string StackId { get; set; }
        public string RequestId { get; set; }
        public string LogicalResourceId { get; set; }
    }

    class CustomResourceResponse<T> : CustomResourceResponse
    {
        public T Data { get; set; }
    }
}
