namespace LocalDataApi.Domain.WeChatWork
{
    public class DepartmentTreeNode
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public long ParentId { get; set; }
        public long Order { get; set; }
        public List<string> DepartmentLeader { get; set; }
        public List<DepartmentTreeNode> Children { get; set; }
    }
}