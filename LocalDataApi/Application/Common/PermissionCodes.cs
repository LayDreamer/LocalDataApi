namespace LocalDataApi.Application.Common
{
    /// <summary>
    /// RBAC 权限编码常量表(与数据库 Permission.Code 保持一致)。
    /// 编码格式: 模块.资源.动作。禁止在代码中硬编码权限字符串,统一引用本类。
    /// </summary>
    public static class PermissionCodes
    {
        // ========== Identity 用户管理 ==========
        public const string UserView = "Identity.User.View";
        public const string UserCreate = "Identity.User.Create";
        public const string UserUpdate = "Identity.User.Update";
        public const string UserDelete = "Identity.User.Delete";
        public const string UserAssignRole = "Identity.User.AssignRole";

        // ========== Identity 员工账号绑定 ==========
        public const string EmployeeBindUser = "Identity.Employee.BindUser";

        // ========== Identity 角色管理 ==========
        public const string RoleView = "Identity.Role.View";
        public const string RoleCreate = "Identity.Role.Create";
        public const string RoleUpdate = "Identity.Role.Update";
        public const string RoleDelete = "Identity.Role.Delete";
        public const string RoleAssignPermission = "Identity.Role.AssignPermission";

        // ========== Identity 权限管理 ==========
        public const string PermissionView = "Identity.Permission.View";
        public const string PermissionUpdate = "Identity.Permission.Update";

        // ========== Identity 部门管理 ==========
        public const string DepartmentView = "Identity.Department.View";
        public const string DepartmentSync = "Identity.Department.Sync";

        // ========== Platform menu management ==========
        public const string PlatformMenuView = "Platform.Menu.View";
        public const string PlatformMenuCreate = "Platform.Menu.Create";
        public const string PlatformMenuUpdate = "Platform.Menu.Update";
        public const string PlatformMenuDelete = "Platform.Menu.Delete";

        // ========== Platform 岗位管理 ==========
        public const string PlatformPositionView = "Platform.Position.View";
        public const string PlatformPositionCreate = "Platform.Position.Create";
        public const string PlatformPositionEdit = "Platform.Position.Edit";
        public const string PlatformPositionDelete = "Platform.Position.Delete";

        // ========== System 数据字典中心 ==========
        public const string SystemDictionaryView = "System.Dictionary.View";
        public const string SystemDictionaryCreate = "System.Dictionary.Create";
        public const string SystemDictionaryUpdate = "System.Dictionary.Update";
        public const string SystemDictionaryDelete = "System.Dictionary.Delete";

        // ========== Platform 统一编码规则(NumberRule) ==========
        public const string PlatformNumberRuleView = "Platform.NumberRule.View";
        public const string PlatformNumberRuleCreate = "Platform.NumberRule.Create";
        public const string PlatformNumberRuleUpdate = "Platform.NumberRule.Update";

        // ========== Platform 统一附件(Attachment) ==========
        public const string PlatformAttachmentView = "Platform.Attachment.View";
        public const string PlatformAttachmentUpload = "Platform.Attachment.Upload";
        public const string PlatformAttachmentDelete = "Platform.Attachment.Delete";

        // ========== PMC 排产管理 ==========
        public const string ScheduleView = "PMC.Schedule.View";
        public const string ScheduleCreate = "PMC.Schedule.Create";
        public const string ScheduleUpdate = "PMC.Schedule.Update";
        public const string ScheduleDelete = "PMC.Schedule.Delete";
        public const string SchedulePublish = "PMC.Schedule.Publish";
        public const string ScheduleExport = "PMC.Schedule.Export";

        // ========== PMC 工单管理 ==========
        public const string WorkOrderView = "PMC.WorkOrder.View";
        public const string WorkOrderCreate = "PMC.WorkOrder.Create";
        public const string WorkOrderUpdate = "PMC.WorkOrder.Update";
        public const string WorkOrderDelete = "PMC.WorkOrder.Delete";
        public const string WorkOrderClose = "PMC.WorkOrder.Close";

        // ========== PMC 交期评审 ==========
        public const string DeliveryReviewView = "PMC.DeliveryReview.View";
        public const string DeliveryReviewCreate = "PMC.DeliveryReview.Create";
        public const string DeliveryReviewUpdate = "PMC.DeliveryReview.Update";
        public const string DeliveryReviewApprove = "PMC.DeliveryReview.Approve";
        public const string DeliveryReviewReject = "PMC.DeliveryReview.Reject";

        // ========== PMC 外产管理 ==========
        public const string ExternalProductionView = "PMC.ExternalProduction.View";
        public const string ExternalProductionCreate = "PMC.ExternalProduction.Create";
        public const string ExternalProductionUpdate = "PMC.ExternalProduction.Update";
        public const string ExternalProductionDelete = "PMC.ExternalProduction.Delete";
        public const string ExternalProductionApprove = "PMC.ExternalProduction.Approve";
        public const string ProductView = "PMC.Product.View";

        public const string BlfParameterView = "BLF.Parameter.View";
        public const string BlfParameterCreate = "BLF.Parameter.Create";
        public const string BlfParameterUpdate = "BLF.Parameter.Update";
        public const string BlfParameterDelete = "BLF.Parameter.Delete";

        public const string ErpUserView = "ERP.User.View";
        public const string ErpUserValidate = "ERP.User.Validate";

        // ========== ERP 工单 ==========
        public const string ErpWorkOrderView = "ERP.WorkOrder.View";
        public const string ErpWorkOrderUpdate = "ERP.WorkOrder.Update";

        // ========== ERP 物料 ==========
        public const string ErpMaterialView = "ERP.Material.View";
        public const string ErpMaterialImport = "ERP.Material.Import";
        public const string ErpMaterialExport = "ERP.Material.Export";

        // ========== WeChatWork ==========
        public const string WeChatWorkMessageSend = "WeChatWork.Message.Send";
        public const string WeChatWorkDepartmentView = "WeChatWork.Department.View";
        public const string WeChatWorkDepartmentSync = "WeChatWork.Department.Sync";
        public const string WeChatWorkUserSync = "WeChatWork.User.Sync";
        public const string WeChatWorkSmartSheetView = "WeChatWork.SmartSheet.View";
        public const string WeChatWorkSmartSheetSync = "WeChatWork.SmartSheet.Sync";
        public const string WeChatWorkUserView = "WeChatWork.User.View";
        public const string WeChatWorkGroupChatView = "WeChatWork.GroupChat.View";
        public const string WeChatWorkJsSdkView = "WeChatWork.JsSdk.View";
        public const string SystemTestAccess = "System.Test.Access";
        public const string PlatformLoginLogView = "Platform.LoginLog.View";
        public const string PlatformOperationLogView = "Platform.OperationLog.View";
        public const string PlatformDataChangeLogView = "Platform.DataChangeLog.View";

        /// <summary>
        /// 全部权限编码集合(用于系统管理员 Admin 角色授予与初始化)。
        /// </summary>
        public static IReadOnlyList<string> All
        {
            get
            {
                return new[]
                {
                    // Identity
                    UserView, UserCreate, UserUpdate, UserDelete, UserAssignRole, EmployeeBindUser,
                    RoleView, RoleCreate, RoleUpdate, RoleDelete, RoleAssignPermission,
                    PermissionView, PermissionUpdate,
                    DepartmentView, DepartmentSync,
                    PlatformMenuView, PlatformMenuCreate, PlatformMenuUpdate, PlatformMenuDelete,
                    PlatformPositionView, PlatformPositionCreate, PlatformPositionEdit, PlatformPositionDelete,
                    SystemDictionaryView, SystemDictionaryCreate, SystemDictionaryUpdate, SystemDictionaryDelete,
                    PlatformNumberRuleView, PlatformNumberRuleCreate, PlatformNumberRuleUpdate,
                    PlatformAttachmentView, PlatformAttachmentUpload, PlatformAttachmentDelete,
                    PlatformLoginLogView, PlatformOperationLogView, PlatformDataChangeLogView,
                    // PMC
                    ScheduleView, ScheduleCreate, ScheduleUpdate, ScheduleDelete, SchedulePublish, ScheduleExport,
                    WorkOrderView, WorkOrderCreate, WorkOrderUpdate, WorkOrderDelete, WorkOrderClose,
                    DeliveryReviewView, DeliveryReviewCreate, DeliveryReviewUpdate, DeliveryReviewApprove, DeliveryReviewReject,
                    ExternalProductionView, ExternalProductionCreate, ExternalProductionUpdate, ExternalProductionDelete, ExternalProductionApprove,
                    ProductView,
                    BlfParameterView, BlfParameterCreate, BlfParameterUpdate, BlfParameterDelete,
                    // ERP
                    ErpWorkOrderView, ErpWorkOrderUpdate,
                    ErpMaterialView, ErpMaterialImport, ErpMaterialExport,
                    ErpUserView, ErpUserValidate,
                    // WeChatWork
                    WeChatWorkMessageSend, WeChatWorkDepartmentView, WeChatWorkDepartmentSync,
                    WeChatWorkUserSync, WeChatWorkSmartSheetView, WeChatWorkSmartSheetSync
                    , WeChatWorkUserView, WeChatWorkGroupChatView, WeChatWorkJsSdkView, SystemTestAccess
                };
            }
        }
    }
}
