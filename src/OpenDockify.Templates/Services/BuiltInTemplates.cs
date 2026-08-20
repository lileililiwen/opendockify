using OpenDockify.Templates.Models;

namespace OpenDockify.Templates.Services;

/// <summary>
/// The 8 built-in templates shipped with OpenDockify. They are read-only for
/// users (copy to edit) and are seeded into the database when it is empty.
/// Content is a starting point only — users should review every generated
/// document before use.
/// </summary>
public static class BuiltInTemplates
{
    public sealed record BuiltInTemplateData(
        string Name,
        string Category,
        string Description,
        string RiskNoticeText,
        string Body,
        TemplateDefinition Definition);

    private static readonly BuiltInTemplateData _loanIou = new(
        "借条",
        "借贷",
        "个人之间借款的标准借条，包含利率与还款期限。",
        "本借条由起草工具生成，不构成法律意见。利率约定不得超过法律保护上限，禁止高利放贷（含变相高息）。",
        "今有借款人{{borrowerName}}（身份证号：{{idCard}}）因资金周转需要，向出借人{{lenderName}}借款人民币（大写）{{amount}}，年利率约定为{{annualRate}}%，借款日期为{{loanDate}}，应于{{dueDate}}前一次性还清本息。",
        new TemplateDefinition
        {
            Fields =
            {
                new FieldDefinition { Name = "lenderName", Label = "出借人姓名", Type = FieldType.Text, Required = true },
                new FieldDefinition { Name = "borrowerName", Label = "借款人姓名", Type = FieldType.Text, Required = true },
                new FieldDefinition { Name = "idCard", Label = "借款人身份证号", Type = FieldType.Text, Required = true },
                new FieldDefinition { Name = "amount", Label = "借款金额（元）", Type = FieldType.Currency, Required = true },
                new FieldDefinition { Name = "annualRate", Label = "年利率（%）", Type = FieldType.Number, Required = true, Validation = new ValidationRule { Min = 0, Max = 100, IsInterestRate = true } },
                new FieldDefinition { Name = "loanDate", Label = "借款日期", Type = FieldType.Date, Required = true },
                new FieldDefinition { Name = "dueDate", Label = "还款日期", Type = FieldType.Date, Required = true },
            },
            Clauses =
            {
                new ClauseDefinition { Id = "penalty", Title = "逾期违约金", Text = "如借款人未按期还清本息，逾期部分按每日万分之五支付违约金。" },
                new ClauseDefinition { Id = "dispute", Title = "争议解决", Text = "因本借条产生的争议，双方协商解决；协商不成的，向出借人住所地人民法院起诉。" },
            },
        });

    private static readonly BuiltInTemplateData _generalIou = new(
        "欠条",
        "借贷",
        "确认债务关系的一般欠条。",
        "本欠条由起草工具生成，不构成法律意见。请如实填写欠款事实与金额。",
        "欠款人{{debtorName}}因{{reason}}欠付{{creditorName}}人民币（大写）{{amount}}，承诺于{{dueDate}}前全部还清。",
        new TemplateDefinition
        {
            Fields =
            {
                new FieldDefinition { Name = "debtorName", Label = "欠款人姓名", Type = FieldType.Text, Required = true },
                new FieldDefinition { Name = "creditorName", Label = "债权人姓名", Type = FieldType.Text, Required = true },
                new FieldDefinition { Name = "amount", Label = "欠款金额（元）", Type = FieldType.Currency, Required = true },
                new FieldDefinition { Name = "reason", Label = "欠款事由", Type = FieldType.Text, Required = true },
                new FieldDefinition { Name = "dueDate", Label = "承诺还款日期", Type = FieldType.Date, Required = true },
            },
            Clauses =
            {
                new ClauseDefinition { Id = "penalty", Title = "逾期责任", Text = "到期未还的，欠款人按日支付应还金额万分之五的违约金。" },
            },
        });

    private static readonly BuiltInTemplateData _residentialLease = new(
        "房屋租赁合同",
        "租赁",
        "住宅房屋租赁合同，含押金、租期与费用承担条款。",
        "本模板由起草工具生成，不构成法律意见。租金、押金及维修责任请据实约定并签字确认。",
        "出租人（甲方）{{landlordName}}将坐落于{{address}}的房屋出租给承租人（乙方）{{tenantName}}使用，月租金人民币（大写）{{monthlyRent}}，押金人民币（大写）{{deposit}}，租赁期限自{{startDate}}至{{endDate}}。",
        new TemplateDefinition
        {
            Fields =
            {
                new FieldDefinition { Name = "landlordName", Label = "出租人姓名", Type = FieldType.Text, Required = true },
                new FieldDefinition { Name = "tenantName", Label = "承租人姓名", Type = FieldType.Text, Required = true },
                new FieldDefinition { Name = "address", Label = "房屋地址", Type = FieldType.Text, Required = true },
                new FieldDefinition { Name = "monthlyRent", Label = "月租金（元）", Type = FieldType.Currency, Required = true },
                new FieldDefinition { Name = "deposit", Label = "押金（元）", Type = FieldType.Currency, Required = true },
                new FieldDefinition { Name = "startDate", Label = "起租日期", Type = FieldType.Date, Required = true },
                new FieldDefinition { Name = "endDate", Label = "退租日期", Type = FieldType.Date, Required = true },
            },
            Clauses =
            {
                new ClauseDefinition { Id = "utilities", Title = "费用承担", Text = "租赁期间水、电、燃气、物业等费用由乙方承担，双方另有约定的除外。" },
                new ClauseDefinition { Id = "maintenance", Title = "维修责任", Text = "房屋主体及固定设施的自然损坏由甲方负责维修；乙方人为损坏由乙方承担维修费用。" },
                new ClauseDefinition { Id = "renewal", Title = "续租条款", Text = "租赁期满前30日，双方可协商续租；同等条件下乙方享有优先承租权。" },
            },
        });

    private static readonly BuiltInTemplateData _mutualNda = new(
        "互不披露协议",
        "保密",
        "双方互相保密义务的保密协议。",
        "本协议由起草工具生成，不构成法律意见。保密信息范围请结合实际情况界定。",
        "甲方{{companyA}}与乙方{{companyB}}就合作洽谈过程中相互披露的商业信息签订本协议，保密期限自{{effectiveDate}}起{{termMonths}}个月。",
        new TemplateDefinition
        {
            Fields =
            {
                new FieldDefinition { Name = "companyA", Label = "甲方名称", Type = FieldType.Text, Required = true },
                new FieldDefinition { Name = "companyB", Label = "乙方名称", Type = FieldType.Text, Required = true },
                new FieldDefinition { Name = "effectiveDate", Label = "生效日期", Type = FieldType.Date, Required = true },
                new FieldDefinition { Name = "termMonths", Label = "保密期限（月）", Type = FieldType.Number, Required = true, Validation = new ValidationRule { Min = 0, Max = 120 } },
            },
            Clauses =
            {
                new ClauseDefinition { Id = "scope", Title = "保密信息范围", Text = "保密信息包括双方以口头、书面或电子形式披露的未公开的技术、经营、财务及客户信息。" },
                new ClauseDefinition { Id = "remedy", Title = "违约责任", Text = "任何一方违反保密义务给对方造成损失的，应赔偿因此产生的直接损失。" },
            },
        });

    private static readonly BuiltInTemplateData _outsourcingService = new(
        "外包服务合同",
        "服务",
        "委托方与外包服务方之间的服务合同框架。",
        "本合同由起草工具生成，不构成法律意见。服务范围、交付标准与知识产权归属请具体约定。",
        "委托方{{clientName}}将{{serviceDesc}}外包给服务方{{providerName}}，服务费用为人民币（大写）{{fee}}，服务期间自{{startDate}}至{{endDate}}。",
        new TemplateDefinition
        {
            Fields =
            {
                new FieldDefinition { Name = "clientName", Label = "委托方名称", Type = FieldType.Text, Required = true },
                new FieldDefinition { Name = "providerName", Label = "服务方名称", Type = FieldType.Text, Required = true },
                new FieldDefinition { Name = "serviceDesc", Label = "服务内容", Type = FieldType.Text, Required = true },
                new FieldDefinition { Name = "fee", Label = "服务费用（元）", Type = FieldType.Currency, Required = true },
                new FieldDefinition { Name = "startDate", Label = "开始日期", Type = FieldType.Date, Required = true },
                new FieldDefinition { Name = "endDate", Label = "结束日期", Type = FieldType.Date, Required = true },
            },
            Clauses =
            {
                new ClauseDefinition { Id = "payment", Title = "付款方式", Text = "委托方于服务验收合格后10个工作日内支付服务费用。" },
                new ClauseDefinition { Id = "ip", Title = "知识产权", Text = "服务过程中形成的交付成果的知识产权归委托方所有，另有约定的除外。" },
                new ClauseDefinition { Id = "confidentiality", Title = "保密条款", Text = "双方对合作中知悉的对方商业秘密负有保密义务，未经同意不得向第三方披露。" },
            },
        });

    private static readonly BuiltInTemplateData _partTimeLabor = new(
        "兼职劳务协议",
        "劳务",
        "非全日制兼职劳务协议，约定报酬与工作内容。",
        "本协议由起草工具生成，不构成法律意见。非全日制用工小时计酬标准不得低于当地规定的最低小时工资。",
        "用人单位{{employerName}}聘用{{employeeName}}担任{{position}}岗位兼职工作，报酬标准为每小时人民币（大写）{{hourlyRate}}，约定每周工作{{workHours}}小时，协议期间为{{period}}。",
        new TemplateDefinition
        {
            Fields =
            {
                new FieldDefinition { Name = "employerName", Label = "用人单位名称", Type = FieldType.Text, Required = true },
                new FieldDefinition { Name = "employeeName", Label = "劳动者姓名", Type = FieldType.Text, Required = true },
                new FieldDefinition { Name = "position", Label = "岗位", Type = FieldType.Text, Required = true },
                new FieldDefinition { Name = "hourlyRate", Label = "小时报酬（元）", Type = FieldType.Currency, Required = true },
                new FieldDefinition { Name = "workHours", Label = "每周工作小时数", Type = FieldType.Number, Required = true, Validation = new ValidationRule { Min = 0, Max = 24 } },
                new FieldDefinition { Name = "period", Label = "协议期间", Type = FieldType.Text, Required = true },
            },
            Clauses =
            {
                new ClauseDefinition { Id = "payment", Title = "报酬支付", Text = "报酬按实际工作小时数按月结算，于次月15日前支付。" },
                new ClauseDefinition { Id = "termination", Title = "协议解除", Text = "任何一方可提前3日书面通知对方解除本协议，报酬结算至解除之日。" },
            },
        });

    private static readonly BuiltInTemplateData _repaymentConfirmation = new(
        "还款确认书",
        "借贷",
        "确认分期还款安排与已还款金额的确认书。",
        "本确认书由起草工具生成，不构成法律意见。确认金额与期数请以实际履行情况为准。",
        "债务人{{debtorName}}确认尚欠债权人{{creditorName}}借款本金及利息共计人民币（大写）{{remainingAmount}}，双方确认截至{{confirmDate}}已还款人民币（大写）{{totalAmount}}，剩余款项按约定分期偿还。",
        new TemplateDefinition
        {
            Fields =
            {
                new FieldDefinition { Name = "debtorName", Label = "债务人姓名", Type = FieldType.Text, Required = true },
                new FieldDefinition { Name = "creditorName", Label = "债权人姓名", Type = FieldType.Text, Required = true },
                new FieldDefinition { Name = "totalAmount", Label = "已还款金额（元）", Type = FieldType.Currency, Required = true },
                new FieldDefinition { Name = "remainingAmount", Label = "尚欠金额（元）", Type = FieldType.Currency, Required = true },
                new FieldDefinition { Name = "confirmDate", Label = "确认日期", Type = FieldType.Date, Required = true },
            },
            Clauses =
            {
                new ClauseDefinition { Id = "installment", Title = "分期安排", Text = "剩余款项自{{confirmDate}}起每月偿还，直至清偿完毕。" },
            },
        });

    private static readonly BuiltInTemplateData _demandLetter = new(
        "催款函",
        "催收",
        "向债务人发出的正式书面催款通知。",
        "本函由起草工具生成，不构成法律意见。发出前请核对债权金额与到期日。",
        "致{{debtorName}}：截至本函发出之日，贵方尚欠我方（{{creditorName}}）人民币（大写）{{amount}}，该款项已于{{dueDate}}到期。请于收到本函之日起15日内清偿。",
        new TemplateDefinition
        {
            Fields =
            {
                new FieldDefinition { Name = "debtorName", Label = "债务人姓名/名称", Type = FieldType.Text, Required = true },
                new FieldDefinition { Name = "creditorName", Label = "债权人姓名/名称", Type = FieldType.Text, Required = true },
                new FieldDefinition { Name = "amount", Label = "欠款金额（元）", Type = FieldType.Currency, Required = true },
                new FieldDefinition { Name = "dueDate", Label = "到期日期", Type = FieldType.Date, Required = true },
                new FieldDefinition { Name = "demandDate", Label = "函件日期", Type = FieldType.Date, Required = true },
            },
            Clauses =
            {
                new ClauseDefinition { Id = "consequence", Title = "法律后果提示", Text = "逾期仍未清偿的，我方将依法采取包括诉讼在内的追偿措施，由此产生的费用由贵方承担。" },
            },
        });

    // Declared after the data fields so static initialization order is correct.
    public static IReadOnlyList<BuiltInTemplateData> All { get; } = new List<BuiltInTemplateData>
    {
        _loanIou,
        _generalIou,
        _residentialLease,
        _mutualNda,
        _outsourcingService,
        _partTimeLabor,
        _repaymentConfirmation,
        _demandLetter,
    };
}
