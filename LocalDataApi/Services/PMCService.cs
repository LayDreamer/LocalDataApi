using LocalDataApi.Data;
using LocalDataApi.Dto;
using LocalDataApi.Exceptions;
using LocalDataApi.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace LocalDataApi.Services
{
    public class PMCService : IPMCService
    {
        private readonly AppDbContext _context;
        public PMCService(AppDbContext context)
        {
            _context = context;
        }

        // 获取外销合同产品列表(测试根据分析单号来获取)
        public async Task<List<PMCProductInfo>> GetPMCProductListInfo(PMCRequestDto request)
        {
            var query = _context.外销合同产品
                .AsNoTracking()
                .Where(e => e.分析单号 == "PCZY126030646" && e.层 == "0")
                .AsQueryable();
            if (!string.IsNullOrEmpty(request.分析单号))
            {
                query = query.Where(e => e.分析单号 == request.分析单号);
            }
            if (!string.IsNullOrEmpty(request.合同号))
            {
                query = query.Where(e => e.合同号 == request.合同号);
            }
            if (!string.IsNullOrEmpty(request.排产编号))
            {
                query = query.Where(e => e.排产编号 == request.排产编号);
            }
            if (!string.IsNullOrEmpty(request.货号))
            {
                query = query.Where(e => e.货号 == request.货号);
            }
            if (!string.IsNullOrEmpty(request.线圈货号))
            {
                query = query.Where(e => e.线圈 == request.线圈货号);
            }

            var total = await query.CountAsync();

            if (total == 0)
            {
                return new();
            }
            var data = await query.ToListAsync();
            return data;
        }

        #region PMC交期评审相关

        // 获取外销合同客户产品列表
        public async Task<List<PMCUserProductInfo>> GetPMCUserProductInfoList(PMCRequestDto request)
        {
            var query = _context.外销合同客户产品
               .AsNoTracking()
               .AsQueryable();

            // 只获取最近6个月的数据
            var sixMonthsAgo = DateTime.Now.AddMonths(-6);
            var sixMonthsAgoStr = sixMonthsAgo.ToString("yyyy-MM-dd");

            // query = query.Where(e => e.合同号 == "SNZJY2603117");

            // 使用字符串比较来过滤日期
            query = query.Where(e => string.Compare(e.创建时间, sixMonthsAgoStr) >= 0);

            // // 合同号过滤
            // if (!string.IsNullOrEmpty(request.合同号))
            // {
            //     query = query.Where(e => e.合同号.Contains(request.合同号));
            // }

            // 限制返回1000条数据
            var data = await query.ToListAsync();
            return data;
        }


        public async Task<List<PMCDeliveryReview>> ConvertToPMCDeliveryReviewList(PMCRequestDto request)
        {
            // 参考 GetPMCUserProductInfoList 方法的实现
            var userProductInfos = await GetPMCUserProductInfoList(request);
            return await ConvertToPMCDeliveryReviewList(userProductInfos);
        }

        // 根据PMC客户产品列表转换为交期评审信息
        public async Task<List<PMCDeliveryReview>> ConvertToPMCDeliveryReviewList(List<PMCUserProductInfo> userProductInfos)
        {
            try
            {
                // 预分配List容量，避免动态扩容
                List<PMCDeliveryReview> data = new List<PMCDeliveryReview>(userProductInfos.Count);
                string orderUser = "ZY1[张圆]";
                if (userProductInfos == null || userProductInfos.Count == 0)
                {
                    return data;
                }

                // 查询外产_订单表中状态为已评审的记录，使用HashSet提高查找效率
                var reviewedOrders = await _context.外产_订单
                    .AsNoTracking()
                    .Where(e => e.状态 == "评审通过")
                    .Select(e => e.排产编号)
                    .ToListAsync();
                // 转换为HashSet，将查找时间复杂度从O(n)降为O(1)，并排除null
                var reviewedOrdersSet = new HashSet<string>(reviewedOrders.Where(x => x != null)!.Cast<string>());
                // 提取查询需要的货号列表
                var itemNos = userProductInfos.Select(e => e.货号).Where(e => !string.IsNullOrEmpty(e)).Distinct().ToList();
                var sourceInfoDict = new Dictionary<string, string>();
                if (itemNos.Count > 0)
                {
                    var sourceInfo = await _context.产品资料
                        .AsNoTracking()
                        .Select(e => new { e.货号, e.制造方式 })
                        .Distinct()
                        .ToListAsync();

                    // 构建字典，便于快速查询
                    foreach (var info in sourceInfo)
                    {
                        if (!string.IsNullOrEmpty(info.货号) && itemNos.Contains(info.货号))
                        {
                            sourceInfoDict[info.货号] = info.制造方式 ?? "";
                        }
                    }
                }

                // var analysisNum = await GenerateAnalysisOrderNumberAsync(orderUser);

                // ===== 在内存中处理数据转换 =====
                foreach (var item in userProductInfos)
                {
                    string schedulingNumber = $"{item.合同号}-{item.序号}";
                    string source = sourceInfoDict.GetValueOrDefault(item.货号) ?? "";
                    //string source="";
                    // 检查是否存在排产编号一致且状态为已评审的记录，使用HashSet提高查找效率
                    if (reviewedOrdersSet.Contains(schedulingNumber))
                    {
                        // 如果存在，跳过该记录
                        continue;
                    }
                    // 计算工单工号
                    string workOrder = "";
                    if (!string.IsNullOrEmpty(item.编号))
                    {
                        // 按照规则处理：USR替换成10，然后去掉中间的字母
                        workOrder = item.编号.ToUpper().Replace("USR", "10");
                        // 只保留数字
                        workOrder = new string(workOrder.Where(char.IsDigit).ToArray());
                    }

                    // 计算线圈货号
                    string coilNumber = string.Empty;
                    if (!string.IsNullOrEmpty(item.货号))
                    {
                        // 提取括号内的内容
                        int startIndex = item.货号.IndexOf('(');
                        int endIndex = item.货号.IndexOf(')');
                        if (startIndex >= 0 && endIndex > startIndex)
                        {
                            coilNumber = item.货号.Substring(startIndex + 1, endIndex - startIndex - 1);
                        }
                    }
                    PMCDeliveryReview review = new PMCDeliveryReview
                    {
                        编号 = item.编号,
                        用户编号 = item.用户编号,
                        用户铭 = item.用户铭,
                        修改状态 = item.修改状态,
                        锁定用户 = item.锁定用户,
                        审核过程 = item.审核过程,
                        打印 = item.打印,
                        合同号 = item.合同号,
                        货号 = item.货号,
                        中文品名 = item.中文品名,
                        中文规格 = item.中文规格,
                        创建时间 = item.创建时间,
                        电压 = item.电压,
                        排产编号 = schedulingNumber,
                        交货日期 = item.货好日期,
                        数量=item.数量,
                        工单单号 = workOrder,
                        线圈货号 = coilNumber,
                        来源 = source,
                        状态 = "待评审",
                        排产用户 = orderUser
                    };
                    data.Add(review);
                }
                return data;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ConvertToPMCDeliveryReviewList 方法异常: {ex.Message}");
                throw;
            }
        }

        // 新增PMC交期评审信息
        public async Task<PMCDeliveryReview> AddPMCDeliveryReview(PMCDeliveryReview deliveryReview)
        {
            if (deliveryReview == null)
            {
                throw new ArgumentNullException(nameof(deliveryReview), "产品信息不能为空");
            }

            // 可根据业务需求增加字段非空校验，例如：
            if (string.IsNullOrWhiteSpace(deliveryReview.合同号))
            {
                throw new ArgumentException("合同号不能为空");
            }
            // 根据编号查询是否已存在
            var existing = await _context.外产_订单
                .FirstOrDefaultAsync(x => x.编号 == deliveryReview.编号);
            if (existing != null)
            {
                // 更新现有实体：将传入实体的所有属性值复制到现有实体
                _context.Entry(existing).CurrentValues.SetValues(deliveryReview);
                // 注意：如果某些字段不需要更新，可以单独赋值
                // 例如：existing.状态 = deliveryReview.状态; existing.最终交期 = deliveryReview.最终交期; ...
            }
            else
            {
                //创建新的排产分析单号
                 //var analysisNum = await GenerateAnalysisOrderNumberAsync(deliveryReview.排产用户);

                // 创建排产分析单并保存到数据库
                //  var scheduling = await SaveSchedulingAnalysisAsync(deliveryReview, analysisNum);

                // 使用排产分析单的编号作为交期评审的编号
                // deliveryReview.编号 = scheduling.编号; 
                
                // deliveryReview.分析单号 = analysisNum;

                deliveryReview.编号= Guid.NewGuid().ToString();

                // 新增
                await _context.外产_订单.AddAsync(deliveryReview);

                //测试用-插入5条新记录：复制deliveryReview的值，交货日期依次+1天，重新生成编号
                // for (int i = 1; i <= 5; i++)
                // {
                //     var newDeliveryReview = new PMCDeliveryReview
                //     {
                //         编号 = Guid.NewGuid().ToString(),
                //         用户编号 = deliveryReview.用户编号,
                //         用户铭 = deliveryReview.用户铭,
                //         修改状态 = deliveryReview.修改状态,
                //         锁定用户 = deliveryReview.锁定用户,
                //         审核过程 = deliveryReview.审核过程,
                //         打印 = deliveryReview.打印,
                //         合同号 = deliveryReview.合同号,
                //         货号 = deliveryReview.货号,
                //         中文品名 = deliveryReview.中文品名,
                //         中文规格 = deliveryReview.中文规格,
                //         创建时间 = deliveryReview.创建时间,
                //         电压 = deliveryReview.电压,
                //         排产编号 = deliveryReview.排产编号,
                //         交货日期 = CalculateDeliveryDate(deliveryReview.交货日期, i),
                //         数量 = deliveryReview.数量,
                //         工单单号 = deliveryReview.工单单号,
                //         线圈货号 = deliveryReview.线圈货号,
                //         来源 = deliveryReview.来源,
                //         状态 = deliveryReview.状态,
                //         排产用户 = deliveryReview.排产用户,
                //         物料货号 = deliveryReview.物料货号,
                //         备注 = deliveryReview.备注
                //     };
                //     await _context.外产_订单.AddAsync(newDeliveryReview);
                // }
            }

            // 保存到数据库
            await _context.SaveChangesAsync();

            // 返回更新或新增后的实体（如果是更新，返回 existing 更准确）
            return existing ?? deliveryReview;
        }

        //PMC交期评审列表
        public async Task<List<PMCDeliveryReview>> GetPMCDeliveryReviewList(PMCRequestDto request)
        {
            var query = _context.外产_订单
               .AsNoTracking()
               .AsQueryable();
            if (!string.IsNullOrEmpty(request.合同号))
            {
                query = query.Where(e => e.合同号 == request.合同号);
            }

            var data = await query.ToListAsync();
            return data;
        }

        #endregion

        #region  PMC产品销控表相关

        //查询PMC产品销控表
        public async Task<List<PMCSalesControl>> GetPMCSalesControlList(string? number)
        {
            if (string.IsNullOrWhiteSpace(number))
            {
                return new List<PMCSalesControl>();
            }
            
            return await _context.产品销控表
               .Where(e => e.货号 == number)
               .AsNoTracking()
               .ToListAsync();
        }

        //添加PMC产品销控列表
        public async Task<List<PMCSalesControl>> AddPMCSalesControlList()
        {
            // 1. 加载评审数据
            var reviewList = await _context.外产_订单
                .Where(e => e.状态 == "评审通过")
                .AsNoTracking()
                .ToListAsync();

            if (!reviewList.Any()) return new List<PMCSalesControl>();

            // 2. 准备过滤条件
            var contractNos = reviewList.Select(r => r.合同号).Where(h => !string.IsNullOrEmpty(h)).Distinct().ToList();
            var allItemNos = reviewList.Select(r => r.货号)
                .Concat(reviewList.Select(r => r.物料货号))
                .Where(h => !string.IsNullOrEmpty(h))
                .Distinct()
                .ToList();

            // 3. 查询产品和仓库数据
            var productTask = await _context.外销合同产品
                .Where(p => contractNos.Contains(p.合同号) && allItemNos.Contains(p.货号))
                .AsNoTracking()
                .Select(p => new PMCProductInfo
                {
                    合同号 = p.合同号,
                    货号 = p.货号,
                    数量 = p.数量,
                    发运数量 = p.发运数量,
                    在产需求量 = p.在产需求量
                })
                .ToListAsync();

            var warehouseTask = await _context.仓库货品
                .Where(w => allItemNos.Contains(w.货号))
                .AsNoTracking()
                .Select(w => new { w.货号, w.数量 })
                .ToListAsync();

            // 4. 数据预处理（内存中）
            var productDict = productTask
                .Where(p => p.合同号 != null && p.货号 != null)
                .GroupBy(p => (p.合同号!, p.货号!))
                .ToDictionary(g => g.Key, g => g.ToList());

            var warehouseDict = warehouseTask
                .Where(w => w.货号 != null)
                .GroupBy(w => w.货号!)
                .ToDictionary(g => g.Key, g => g.Max(x => ParseInt(x.数量)));

            // 5. 生成中间记录（使用匿名对象，避免中间过程的 JSON 转换）
            var intermediateRecords = new List<IntermediateData>();

            foreach (var review in reviewList)
            {
                // 成品
                intermediateRecords.Add(CreateIntermediate(review, review.货号, "成品", productDict, warehouseDict));

                // 半成品
                // if (!string.IsNullOrEmpty(review.物料货号) && review.物料货号 != review.货号)
                // {
                //     intermediateRecords.Add(CreateIntermediate(review, review.物料货号, "半成品", productDict, warehouseDict));
                // }
            }

            // 6. 按货号分组合并
            var result = intermediateRecords
                .GroupBy(r => r.货号)
                .Select(g =>
                {
                    var first = g.First();
                    // 收集所有交货计划（尚未合并）
                
                    var allPlans = g.SelectMany(x => x.交货计划 ?? new List<DeliveryPlan>()).ToList();

                    // ========= 关键修改：按交货日期合并计划 =========
                    var aggregatedPlans = allPlans
                        .GroupBy(p => p.交货日期)
                        .Select(grp => new
                        {
                            交货日期 = grp.Key,
                            交货数量 = grp.Sum(p => int.Parse(p.交货数量)).ToString(),
                            状态 = "",  // 状态由前端计算，后端留空
                            排产用户 = grp.FirstOrDefault(p => !string.IsNullOrEmpty(p.排产用户))?.排产用户 ?? ""
                        })
                        .ToList();
                                            
                    // 序列化为 JSON 字符串
                    string deliveryPlanJson = JsonConvert.SerializeObject(aggregatedPlans);
                    string? parentItemNo = first.父级货号 == first.货号 ? "" : first.父级货号;

                    return new PMCSalesControl
                    {
                        编号 = Guid.NewGuid().ToString(),
                        合同号 = first.合同号,
                        排产编号 = first.排产编号,
                        货号 = g.Key,
                        父级货号 = first.父级货号,
                        物料货号 = first.物料货号,
                        中文品名 = first.中文品名,
                        中文规格 = first.中文规格,
                        分析单号 = first.分析单号,
                        商品属性 = first.商品属性,
                        // 数值字段直接 Sum
                        在产数 = g.Sum(x => x.在产数).ToString(),
                        订单总需求 = g.Sum(x => x.需求量).ToString(),
                        仓库数 = g.Sum(x => x.仓库数).ToString(),
                        // 使用合并后的交货计划
                        交货计划 = deliveryPlanJson
                    };
                })
                .ToList();

            // 7. 更新或插入数据库
            try
            {
                // 过滤掉货号为null的记录
                var validResult = result.Where(r => !string.IsNullOrEmpty(r.货号)).ToList();

                if (validResult.Count == 0)
                {
                    return result;
                }

                // 提取有效的货号列表
                var validItemNos = validResult.Select(r => r.货号).ToList();

                // 查询现有记录，过滤掉货号为null的记录
                var existingItems = await _context.产品销控表
                    .Where(x => validItemNos.Contains(x.货号) && x.货号 != null)
                    .AsNoTracking()
                    .ToListAsync();

                // 手动构建字典，确保键不为null
                var existingDict = existingItems
                    .Where(x => x.货号 != null)
                    .ToDictionary(x => x.货号!);

                foreach (var newItem in validResult)
                {
                    if (existingDict.TryGetValue(newItem.货号, out var existing))
                    {
                        // 更新已有记录（保留编号不变）
                        existing.在产数 = newItem.在产数;
                        existing.订单总需求 = newItem.订单总需求;
                        existing.仓库数 = newItem.仓库数;
                        existing.交货计划 = newItem.交货计划;
                        // 其他字段按需更新
                        _context.Entry(existing).State = EntityState.Modified;
                    }
                    else
                    {
                        _context.产品销控表.Add(newItem);
                    }
                }

                // 8. 保存外产_发运数据
                var validIntermediate = intermediateRecords
                    .Where(r => !string.IsNullOrEmpty(r.货号) && !string.IsNullOrEmpty(r.排产编号))
                    .ToList();

                if (validIntermediate.Any())
                {
                    var shipmentList = validIntermediate.Select(item => new ExternalProductionShipment
                    {
                        合同号 = item.合同号,
                        货号 = item.货号,
                        排产编号 = item.排产编号,
                        需求量 = item.需求量.ToString(),
                        发运数量 = item.发运数量.ToString(),
                    }).ToList();

                    await AddOrUpdateExternalProductionShipmentList(shipmentList);
                }

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                var innerMessage = ex.InnerException?.Message;
                Console.WriteLine($"Inner Exception: {innerMessage}");
                throw;
            }

            return result;
        }


        private IntermediateData CreateIntermediate(
            PMCDeliveryReview review,
            string itemNo,
            string attr,
            Dictionary<(string, string), List<PMCProductInfo>> dict,
            Dictionary<string, int> houseDict)
        {
            var key = (review.合同号, itemNo);
            int totalDemand = 0;
            int totalInProd = 0;
            int totalShipped = 0;
            var plans = new List<DeliveryPlan>();

            if (dict.TryGetValue(key, out var products))
            {
                foreach (var p in products)
                {
                    int q = ParseInt(p.数量);
                    int s = ParseInt(p.发运数量);
                    totalDemand += (q - s);
                    totalInProd += ParseInt(p.在产需求量);
                    totalShipped += s;
                    plans.Add(new DeliveryPlan { 交货日期 = review.交货日期, 交货数量 = p.数量, 排产用户 = review.排产用户 });
                }
            }

            houseDict.TryGetValue(itemNo, out int houseQty);

            return new IntermediateData
            {
                合同号 = review.合同号,
                排产编号 = review.排产编号,
                货号 = itemNo,
                父级货号 = review.货号,
                物料货号 = review.物料货号,
                中文品名 = review.中文品名,
                中文规格 = review.中文规格,
                分析单号 = review.分析单号,
                商品属性 = attr,
                在产数 = totalInProd,
                需求量 = totalDemand,
                发运数量 = totalShipped,
                仓库数 = houseQty,
                排产用户 = review.排产用户,
                交货计划 = plans
            };
        }

        private static int ParseInt(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            // 性能优化：直接遍历字符串提取数字
            int result = 0;
            foreach (char c in s)
            {
                if (char.IsDigit(c))
                    result = result * 10 + (c - '0');
            }
            return result;
        }

        /// <summary>
        /// 计算交货日期：在原日期基础上增加指定天数
        /// </summary>
        private static string CalculateDeliveryDate(string? originalDate, int daysToAdd)
        {
            if (string.IsNullOrEmpty(originalDate))
                return "";

            if (DateTime.TryParse(originalDate, out var date))
                return date.AddDays(daysToAdd).ToString("yyyy-MM-dd");

            return originalDate;
        }

        #endregion

        #region 系统原始排产分析单相关

        // 根据排产用户生成分析单号        
        public async Task<string> GenerateAnalysisOrderNumberAsync(string productionUser)
        {
            // 1. 提取排产用户代码（取 '[' 之前的部分）
            string userCode = productionUser?.Split('[')[0];
            if (string.IsNullOrWhiteSpace(userCode))
            {
                throw new ArgumentException("排产用户格式不正确，无法提取代码", nameof(productionUser));
            }

            // 2. 当前年月，格式 yyMM（如 2603）
            string yearMonth = DateTime.Now.ToString("yyMM");

            // 3. 构造前缀：PC + 用户代码 + 年月
            string prefix = $"PC{userCode}{yearMonth}";

            // 4. 查询数据库中最大的流水码（即最后4位数字）
            var existingNumbers = await _context.排产分析单
                .Where(x => x.分析单号.StartsWith(prefix))
                .Select(x => x.分析单号)
                .ToListAsync();

            int maxSerial = 0;
            foreach (var number in existingNumbers)
            {
                // 提取前缀之后的剩余部分
                string suffix = number.Substring(prefix.Length);

                // 尝试将剩余部分转换为整数（忽略非数字部分，但这里假设剩余部分全是数字）
                if (int.TryParse(suffix, out int serial) && serial > maxSerial)
                {
                    maxSerial = serial;
                }
            }
            int newSerial = maxSerial + 1;
            string serialStr = newSerial.ToString("D4");
            return $"{prefix}{serialStr}";
        }

        // 根据排产用户和分析单号保存排产分析单信息
        public async Task<SchedulingAnalysis> SaveSchedulingAnalysisAsync(PMCDeliveryReview deliveryReview, string analysisNum)
        {
            string nowss = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string nowdd = DateTime.Now.ToString("yyyy-MM-dd");
            string productionUser = deliveryReview.排产用户;
            string userNum = await GetUserNumberAsync(productionUser);

            string prefix = $"{userNum}UNT";
            var existingNumbers = await _context.排产分析单
                .Where(x => x.编号.StartsWith(prefix))
                .Select(x => x.编号)
                .ToListAsync();

            int maxSerial = 0;
            foreach (var number in existingNumbers)
            {
                // 提取前缀之后的剩余部分
                string suffix = number.Substring(prefix.Length);

                // 尝试将剩余部分转换为整数（忽略非数字部分，但这里假设剩余部分全是数字）
                if (int.TryParse(suffix, out int serial) && serial > maxSerial)
                {
                    maxSerial = serial;
                }
            }

            // 新流水码 = 最大值 + 1，可根据业务补足位数（例如补零到5位）
            int newSerial = maxSerial + 1;
            string serialStr = newSerial.ToString("D5"); // 假设需要5位，不足补零
            string newNumber = $"{prefix}{serialStr}";

            var scheduling = new SchedulingAnalysis
            {
                编号 = newNumber,
                用户铭 = productionUser,
                用户编号 = userNum,
                修改状态 = "0",
                创建时间 = nowss,
                锁定用户 = productionUser?.Split('[')[0],
                审核过程 = $"{productionUser}-{nowss}",
                分析单号 = analysisNum,
                分析人 = productionUser,
                分析日期 = nowdd,
                排产编号 = deliveryReview.排产编号,
            };
            //测试时先不保存到数据库，等后续逻辑完善后再保存
            // await _context.排产分析单.AddAsync(scheduling);
            return scheduling;
        }

        // 根据排产用户获取用户编号
        public async Task<string> GetUserNumberAsync(string productionUser)
        {
            // 1. 提取排产用户代码（取 '[' 之前的部分）
            string userCode = productionUser?.Split('[')[0];
            if (string.IsNullOrWhiteSpace(userCode))
            {
                throw new ArgumentException("排产用户格式不正确，无法提取代码", nameof(productionUser));
            }
            var userNumber = await _context.tb_control_user
               .Where(x => x.usercode == userCode)
               .Select(x => x.ID).FirstOrDefaultAsync();
            return userNumber.Trim();
        }

        #endregion


        #region 转换的排产分析详情列表

        // 根据销控表中的货号获取排产分析详情列表（嵌套树形结构）
        public async Task<List<SchedulingAnalysisDto>> GetSchedulingAnalysisListDto(PMCRequestDto request)
        {
            var result = new List<SchedulingAnalysisDto>();

            // 基础数据
            var salesControlList = await GetPMCSalesControlList(request.货号);
            var salesData = salesControlList?.FirstOrDefault();
            string schedulingNo = salesData?.排产编号;

            // 第一步：递归获取所有层级的产品装配清单（树形结构）
            var assemblyTree = await GetAssemblyTreeNested(request.货号);

            // 第二步：收集所有货号（父 + 所有层级子级）
            var itemNos = new HashSet<string>();
            if (!string.IsNullOrEmpty(request.货号))
                itemNos.Add(request.货号);

            CollectAllItemNos(assemblyTree, itemNos);

            // 第三步：批量查询所有数据（只查询一次）
            var productDataDict = await GetProductDataBatchAsync(itemNos.ToList());
            var warehouseGoodsDict = await GetWarehouseGoodsBatchAsync(itemNos.ToList(), request.货号);
            var productionDemandDict = await GetProductionDemandBatchAsync(itemNos.ToList(), schedulingNo);
            var inTransitQuantityDict = await GetInTransitQuantityBatchAsync(itemNos.ToList(), schedulingNo);
            //   var  productionDemandDict=new Dictionary<string, ProductionDemand>();
            //   var inTransitQuantityDict = new Dictionary<string, InTransitQuantity>();
            
            // 第四步：构建嵌套树形结果
            var parentDto = BuildDto(request.货号, 0, null, salesData, 
                productDataDict, warehouseGoodsDict, productionDemandDict, inTransitQuantityDict);
            
            // 构建子级嵌套结构
            parentDto.子集 = BuildNestedDtoList(assemblyTree, 1, 
                productDataDict, warehouseGoodsDict, productionDemandDict, inTransitQuantityDict);

            result.Add(parentDto);
            return result;
        }

        /// <summary>
        /// 递归收集所有货号
        /// </summary>
        private void CollectAllItemNos(List<AssemblyNode>? nodes, HashSet<string> itemNos)
        {
            if (nodes == null || nodes.Count == 0)
                return;

            foreach (var node in nodes)
            {
                if (!string.IsNullOrEmpty(node.Assembly.货号))
                    itemNos.Add(node.Assembly.货号);

                // 递归收集子级货号
                CollectAllItemNos(node.Children, itemNos);
            }
        }

        /// <summary>
        /// 构建单个Dto
        /// </summary>
        private SchedulingAnalysisDto BuildDto(
            string itemNo,
            int level,
            ProductDataAssemblyList? assembly,
            PMCSalesControl? sales,
            Dictionary<string, ProductData> productDataDict,
            Dictionary<string, WarehouseGoods> warehouseGoodsDict,
            Dictionary<string, ProductionDemand> productionDemandDict,
            Dictionary<string, InTransitQuantity> inTransitQuantityDict)
        {
            productDataDict.TryGetValue(itemNo, out var productData);
            warehouseGoodsDict.TryGetValue(itemNo, out var goodsData);
            productionDemandDict.TryGetValue(itemNo, out var productionDemand);
            inTransitQuantityDict.TryGetValue(itemNo, out var inTransit);

            // 计算仓库可用数
            // string AvailableQuantity = (double.Parse(goodsData?.数量 ?? "0") + (inTransit?.在产量 ?? 0) - (productionDemand?.需求量 ?? 0)).ToString();

            return new SchedulingAnalysisDto
            {
                货号 = itemNo,
                层 = level.ToString(),
                品名 = sales?.中文品名 ?? productData?.中文品名,
                规格 = sales?.中文规格 ?? productData?.中文规格,
                成品货号 = sales?.父级货号,
                来源 = assembly?.来源 ?? productData?.制造方式 ?? "",
                用量 = assembly?.用量 ?? "",
                单位 = assembly?.单位 ?? productData?.数量单位 ?? "",
                仓库名称 = goodsData?.仓库名,
                仓库数 = goodsData?.数量,
                库存上限 = goodsData?.库存上限,
                库存下限 = goodsData?.库存下限,             
                产品属性 = productData?.产品属性,
                工序名称 = productData?.工序名称,
                工序车间 = productData?.生产车间,
                在产需求 = productionDemand?.需求量?.ToString(),
                在途数 = inTransit?.在产量?.ToString()
            };
        }

        /// <summary>
        /// 递归构建嵌套Dto列表
        /// </summary>
        private List<SchedulingAnalysisDto>? BuildNestedDtoList(
            List<AssemblyNode>? nodes,
            int level,
            Dictionary<string, ProductData> productDataDict,
            Dictionary<string, WarehouseGoods> warehouseGoodsDict,
            Dictionary<string, ProductionDemand> productionDemandDict,            
            Dictionary<string, InTransitQuantity> inTransitQuantityDict)
            
        {
            if (nodes == null || nodes.Count == 0)
                return null;

            var result = new List<SchedulingAnalysisDto>();

            foreach (var node in nodes)
            {
                var dto = BuildDto(node.Assembly.货号, level, node.Assembly, null,
                    productDataDict, warehouseGoodsDict, productionDemandDict, inTransitQuantityDict);

                // 递归构建子集
                dto.子集 = BuildNestedDtoList(node.Children, level + 1,
                    productDataDict, warehouseGoodsDict, productionDemandDict, inTransitQuantityDict);

                result.Add(dto);
            }
            return result;
        }

        /// <summary>
        /// 装配节点（用于构建树形结构）
        /// </summary>
        private class AssemblyNode
        {
            public ProductDataAssemblyList Assembly { get; set; } = null!;
            public List<AssemblyNode>? Children { get; set; }
        }

        /// <summary>
        /// 递归获取装配清单树（嵌套树形结构）
        /// </summary>
        private async Task<List<AssemblyNode>> GetAssemblyTreeNested(
            string? itemNo,
            HashSet<string>? visitedItemNos = null)
        {
            var result = new List<AssemblyNode>();

            if (string.IsNullOrWhiteSpace(itemNo))
                return result;

            // 初始化已访问集合
            if (visitedItemNos == null)
                visitedItemNos = new HashSet<string>();

            // 预处理货号（去掉括号内容）
            string processedItemNo = itemNo;
            int bracketIndex = itemNo.IndexOf('(');
            if (bracketIndex > 0)
                processedItemNo = itemNo.Substring(0, bracketIndex);

            // 防止循环引用
            if (!visitedItemNos.Add(processedItemNo))
                return result;

            // 获取当前货号的装配信息
            var assemblyList = await GetProductDataAssemblyList(processedItemNo);

            // 批量收集所有货号，一次性查询产品资料获取制造方式和数量单位
            var assemblyItemNos = assemblyList
                .Where(a => !string.IsNullOrEmpty(a.货号))
                .Select(a => a.货号!)
                .Distinct()
                .ToList();

            var sourceDict = new Dictionary<string, string>();
            var unitDict = new Dictionary<string, string>();
            if (assemblyItemNos.Count > 0)
            {
                var productDataList = await _context.产品资料
                    .AsNoTracking()
                    .Where(e => assemblyItemNos.Contains(e.货号))
                    .Select(e => new { e.货号, e.制造方式, e.数量单位 })
                    .ToListAsync();

                foreach (var pd in productDataList)
                {
                    if (!string.IsNullOrEmpty(pd.货号))
                    {
                        sourceDict[pd.货号] = pd.制造方式 ?? "";
                        unitDict[pd.货号] = pd.数量单位 ?? "";
                    }
                }
            }

            foreach (var assembly in assemblyList)
            {
                if (string.IsNullOrEmpty(assembly.货号))
                    continue;

                // 从字典获取制造方式并赋值给来源
                if (sourceDict.TryGetValue(assembly.货号, out var source))
                {
                    assembly.来源 = source;
                }
                // 从字典获取数量单位并赋值给单位
                if (unitDict.TryGetValue(assembly.货号, out var unit))
                {
                    assembly.单位 = unit;
                }

                var node = new AssemblyNode { Assembly = assembly };

                // 如果来源是自制或外协，继续递归获取其子级
                if (!string.IsNullOrEmpty(assembly.来源) && 
                    (assembly.来源 == "自制" || assembly.来源 == "外协"))
                {
                    // 递归获取下一层级
                    node.Children = await GetAssemblyTreeNested(assembly.货号, visitedItemNos);
                }

                result.Add(node);
            }

            return result;
        }


        /// <summary>
        /// 批量获取产品资料
        /// </summary>
        private async Task<Dictionary<string, ProductData>> GetProductDataBatchAsync(List<string> itemNos)
        {
            if (itemNos == null || itemNos.Count == 0)
            {
                return new Dictionary<string, ProductData>();
            }

            var productDataList = await _context.产品资料
                .AsNoTracking()
                .Where(e => itemNos.Contains(e.货号) && !string.IsNullOrEmpty(e.货号))
                .ToListAsync();

            // 使用 GroupBy + First() 处理重复货号，保持与 GetProductData 行为一致
            return productDataList.Where(e => e.货号 != null)
                .GroupBy(e => e.货号!)
                .ToDictionary(g => g.Key, g => g.First());
        }

        /// <summary>
        /// 批量获取仓库货品数据
        /// </summary>
        private async Task<Dictionary<string, WarehouseGoods>> GetWarehouseGoodsBatchAsync(List<string> itemNos, string rootItemNo)
        {
            if (itemNos == null || itemNos.Count == 0)
            {
                return new Dictionary<string, WarehouseGoods>();
            }

            var warehouseGoodsList = await _context.仓库货品
                .AsNoTracking()
                .Where(e => itemNos.Contains(e.货号) && !string.IsNullOrEmpty(e.货号))
                .ToListAsync();

            // 使用 GroupBy + First() 处理重复货号
            // 如果货号 != rootItemNo，则取仓库名为"零件仓库"的第一条数据
            return warehouseGoodsList.Where(e => e.货号 != null)
                .GroupBy(e => e.货号!)
                .ToDictionary(g => g.Key, g =>
                {
                    if (g.Key != rootItemNo)
                    {
                        var 零件仓库 = g.FirstOrDefault(e => e.仓库名 == "零件仓库");
                        return 零件仓库 ?? g.OrderByDescending(e => e.数量).First();
                    }
                    return g.OrderByDescending(e => e.数量).First();
                });
        }

        /// <summary>
        /// 批量获取在产需求量
        /// </summary>
        private async Task<Dictionary<string, ProductionDemand>> GetProductionDemandBatchAsync(List<string> itemNos, string? schedulingNo)
        {
            if (itemNos == null || itemNos.Count == 0)
            {
                return new Dictionary<string, ProductionDemand>();
            }

            var query = _context.在产需求量
                .AsNoTracking()
                .Where(e => itemNos.Contains(e.货号) && !string.IsNullOrEmpty(e.货号));

            if (!string.IsNullOrWhiteSpace(schedulingNo))
            {
                query = query.Where(e => e.排产编号 == schedulingNo);
            }

            var productionDemandList = await query.ToListAsync();

            // 使用 GroupBy + First() 处理重复货号，保持与 GetProductionDemand 行为一致
            return productionDemandList.Where(e => e.货号 != null)
                .GroupBy(e => e.货号!)
                .ToDictionary(g => g.Key, g => g.First());
        }
        /// <summary>
        /// 批量获取在途数
        /// </summary>
        /// <summary>
        /// 批量获取在途数
        /// </summary>
        private async Task<Dictionary<string, InTransitQuantity>> GetInTransitQuantityBatchAsync(List<string> itemNos, string? schedulingNo)
        {
            if (itemNos == null || itemNos.Count == 0)
            {
                return new Dictionary<string, InTransitQuantity>();
            }

            var query = _context.在途数
                .AsNoTracking()
                .Where(e => itemNos.Contains(e.货号) && !string.IsNullOrEmpty(e.货号));

            if (!string.IsNullOrWhiteSpace(schedulingNo))
            {
                query = query.Where(e => e.排产编号 == schedulingNo);
            }

            var inTransitQuantityList = await query.ToListAsync();

            // 使用 GroupBy + First() 处理重复货号，保持与 GetInTransitQuantity 行为一致
            return inTransitQuantityList.Where(e => e.货号 != null)
                .GroupBy(e => e.货号!)
                .ToDictionary(g => g.Key, g => g.First());
        }

        #endregion


        #region 工单管理

        // 获取全部工单列表
        public async Task<List<PMCWorkOrder>> GetPMCWorkOrderList()
        {
            var query = _context.外产_工单
               .AsNoTracking()
               .AsQueryable();
            var data = await query.ToListAsync();
            return data;
        }

        // 更新工单管理
        public async Task<PMCWorkOrder> UpdatePMCWorkOrder(PMCWorkOrder workOrder)
        {
            if (workOrder == null)
            {
                throw new ArgumentNullException(nameof(workOrder), "工单信息不能为空");
            }

            // 可根据业务需求增加字段非空校验，例如：
            if (string.IsNullOrWhiteSpace(workOrder.工单单号))
            {
                throw new ArgumentException("工单单号不能为空");
            }

            // 根据工单单号查询是否存在
            var existing = await _context.外产_工单
                .FirstOrDefaultAsync(x => x.工单单号 == workOrder.工单单号);
            if (existing == null)
            {
                throw new ArgumentException("工单不存在");
            }

            // 更新现有实体：将传入实体的所有属性值复制到现有实体
            _context.Entry(existing).CurrentValues.SetValues(workOrder);

            // 保存到数据库
            await _context.SaveChangesAsync();

            // 返回更新后的实体
            return existing;
        }
        // 创建工单管理
        public async Task<PMCWorkOrder> AddPMCWorkOrder(PMCWorkOrder workOrder)
        {
            if (workOrder == null)
            {
                throw new ArgumentNullException(nameof(workOrder), "工单信息不能为空");
            }

            // 可根据业务需求增加字段非空校验，例如：
            if (string.IsNullOrEmpty(workOrder.工单单号))
            {
                throw new ArgumentException("工单单号不能为空");
            }

            // 使用AsNoTracking提高查询性能
            var existing = await _context.外产_工单
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.工单单号 == workOrder.工单单号);

            if (existing != null)
            {
                // 更新现有实体：将传入实体的所有属性值复制到现有实体
                _context.Entry(existing).CurrentValues.SetValues(workOrder);
            }
            else
            {
                // 新增：创建新对象并复制所有属性
                var newWorkOrder = new PMCWorkOrder
                {
                    编号 = Guid.NewGuid().ToString(),
                    工单单号 = workOrder.工单单号,
                    生产单位 = workOrder.生产单位,
                    成品编号 = workOrder.成品编号,
                    成品品名 = workOrder.成品品名,
                    规格 = workOrder.规格,
                    订单编号 = workOrder.订单编号
                };
                await _context.外产_工单.AddAsync(newWorkOrder);
            }

            // 保存到数据库
            await _context.SaveChangesAsync();

            // 返回更新或新增后的实体
            return workOrder;
        }


        public async Task<PMCWorkOrder> AddPMCWorkOrder(PMCRequestDto requestDto)
        {
            if (requestDto == null)
            {
                throw new ArgumentNullException(nameof(requestDto), "请求参数为空!");
            }

            if (string.IsNullOrWhiteSpace(requestDto.货号))
            {
                throw new ValidationException("货号不能为空");
            }

            // 根据货号在外产_订单表中查找状态为评审通过且物料货号匹配的数据
            var matchedOrder = await _context.外产_订单
                .AsNoTracking()
                .FirstOrDefaultAsync(e =>
                    e.状态 == "评审通过" &&
                    e.物料货号 == requestDto.货号);

            if (matchedOrder == null)
            {
                throw new ValidationException("未找到匹配的外产订单数据");
            }

            // 先查询工单管理表中是否存在成品编号相同的记录
            var existingWorkOrder = await _context.外产_工单
                .FirstOrDefaultAsync(e => e.成品编号 == matchedOrder.货号);

            // 创建新的 PMCWorkOrder 对象
            var workOrder = new PMCWorkOrder();
            if (existingWorkOrder != null)
            {
                // 如果已存在，更新数据
                // workOrder = existingWorkOrder;
                // existingWorkOrder.订单编号 = matchedOrder.合同号;
                // existingWorkOrder.成品编号 = matchedOrder.货号;
                // existingWorkOrder.成品品名 = matchedOrder.中文品名;
                // existingWorkOrder.规格 = matchedOrder.中文规格;
                // existingWorkOrder.工单单号=matchedOrder.工单单号;
                // existingWorkOrder.计划完工日=matchedOrder.交货日期;
                // existingWorkOrder.状态="未下发";
                // _context.Entry(existingWorkOrder).State = EntityState.Modified;
            }

            // 如果找到匹配的数据，可以从中获取一些字段值
            else
            {
                workOrder.编号 = Guid.NewGuid().ToString();
                // 可以根据匹配的数据填充一些字段
                workOrder.订单编号 = matchedOrder.合同号;
                workOrder.成品编号 = matchedOrder.货号;
                workOrder.成品品名 = matchedOrder.中文品名;
                workOrder.规格 = matchedOrder.中文规格;
                workOrder.工单单号 = matchedOrder.工单单号;
                workOrder.计划完工日 = matchedOrder.交货日期;
                workOrder.状态 = "未下发";

                await _context.外产_工单.AddAsync(workOrder);
                await _context.SaveChangesAsync();
            }
            // 保存到数据库


            return workOrder;
        }
        #endregion

        #region 工单销控表

        /// <summary>
        /// 批量添加或更新工单销控表数据（存在则覆盖，不存在则新增）
        /// </summary>
        public async Task<List<WorkOrderSalesControl>> AddOrUpdateWorkOrderSalesControlList(List<WorkOrderSalesControl> list)
        {
            if (list == null || list.Count == 0)
            {
                throw new ArgumentException("工单销控表数据不能为空", nameof(list));
            }

            var result = new List<WorkOrderSalesControl>();
            var itemNos = list
                .Where(x => !string.IsNullOrWhiteSpace(x.货号))
                .Select(x => x.货号!)
                .Distinct()
                .ToList();

            // 查询已存在的记录
            var existingItems = await _context.工单销控表
                .Where(x => itemNos.Contains(x.货号) && x.货号 != null)
                .ToListAsync();

            var existingDict = existingItems
                .Where(x => x.货号 != null)
                .ToDictionary(x => x.货号!);

            foreach (var item in list)
            {
                if (string.IsNullOrWhiteSpace(item.货号))
                {
                    continue;
                }

                if (existingDict.TryGetValue(item.货号, out var existing))
                {
                    // 更新已有记录：先对齐主键编号，再 SetValues，避免 EF Core 检测到主键变更
                    item.编号 = existing.编号;
                    _context.Entry(existing).CurrentValues.SetValues(item);
                    result.Add(existing);
                }
                else
                {
                    // 新增记录
                    if (string.IsNullOrWhiteSpace(item.编号))
                    {
                        item.编号 = Guid.NewGuid().ToString();
                    }
                    await _context.工单销控表.AddAsync(item);
                    result.Add(item);
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        /// <summary>
        /// 根据货号查询工单销控表列表
        /// </summary>
        public async Task<List<WorkOrderSalesControl>> GetWorkOrderSalesControlList(string? itemNo)
        {
            var query = _context.工单销控表.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(itemNo))
            {
                query = query.Where(e => e.货号 == itemNo);
            }

            return await query.ToListAsync();
        }

        #endregion

        #region 工单销控表明细

        /// <summary>
        /// 批量添加或更新工单销控表明细数据（存在则覆盖，不存在则新增）
        /// </summary>
        public async Task<List<WorkOrderSalesControlDetail>> AddOrUpdateWorkOrderSalesControlDetailList(List<WorkOrderSalesControlDetail> list)
        {
            if (list == null || list.Count == 0)
            {
                throw new ArgumentException("工单销控表明细数据不能为空", nameof(list));
            }

            var result = new List<WorkOrderSalesControlDetail>();
            var itemNos = list
                .Where(x => !string.IsNullOrWhiteSpace(x.货号))
                .Select(x => x.货号!)
                .Distinct()
                .ToList();

            // 查询已存在的记录
            var existingItems = await _context.工单销控表明细
                .Where(x => itemNos.Contains(x.货号) && x.货号 != null)
                .ToListAsync();

            var existingDict = existingItems
                .Where(x => x.货号 != null)
                .ToDictionary(x => x.货号!);

            foreach (var item in list)
            {
                if (string.IsNullOrWhiteSpace(item.货号))
                {
                    continue;
                }

                if (existingDict.TryGetValue(item.货号, out var existing))
                {
                    // 更新已有记录：先对齐主键编号，再 SetValues
                    item.编号 = existing.编号;
                    _context.Entry(existing).CurrentValues.SetValues(item);
                    result.Add(existing);
                }
                else
                {
                    // 新增记录
                    if (string.IsNullOrWhiteSpace(item.编号))
                    {
                        item.编号 = Guid.NewGuid().ToString();
                    }
                    await _context.工单销控表明细.AddAsync(item);
                    result.Add(item);
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        /// <summary>
        /// 根据货号查询工单销控表明细列表
        /// </summary>
        public async Task<List<WorkOrderSalesControlDetail>> GetWorkOrderSalesControlDetailList(string? itemNo)
        {
            var query = _context.工单销控表明细.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(itemNo))
            {
                query = query.Where(e => e.货号 == itemNo);
            }

            return await query.ToListAsync();
        }

        /// <summary>
        /// 批量删除工单销控表明细数据
        /// </summary>
        public async Task DeleteWorkOrderSalesControlDetailList(List<string> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                throw new ArgumentException("删除列表不能为空", nameof(ids));
            }

            var items = await _context.工单销控表明细
                .Where(x => ids.Contains(x.编号))
                .ToListAsync();

            if (items.Count > 0)
            {
                _context.工单销控表明细.RemoveRange(items);
                await _context.SaveChangesAsync();
            }
        }

        #endregion

        #region 外产发运

        /// <summary>
        /// 批量添加或更新外产发运数据（存在则覆盖，不存在则新增）
        /// </summary>
        public async Task<List<ExternalProductionShipment>> AddOrUpdateExternalProductionShipmentList(List<ExternalProductionShipment> list)
        {
            if (list == null || list.Count == 0)
            {
                throw new ArgumentException("外产发运数据不能为空", nameof(list));
            }

            var result = new List<ExternalProductionShipment>();
            var keys = list
                .Where(x => !string.IsNullOrWhiteSpace(x.排产编号) && !string.IsNullOrWhiteSpace(x.货号))
                .Select(x => (x.排产编号!, x.货号!))
                .Distinct()
                .ToList();

            var schedulingNos = keys.Select(k => k.Item1).Distinct().ToList();

            // 查询已存在的记录
            var existingItems = await _context.外产_发运
                .Where(x => schedulingNos.Contains(x.排产编号) && x.排产编号 != null)
                .ToListAsync();

            var existingDict = existingItems
                .Where(x => x.排产编号 != null && x.货号 != null)
                .ToDictionary(x => (x.排产编号!, x.货号!));

            foreach (var item in list)
            {
                if (string.IsNullOrWhiteSpace(item.排产编号) || string.IsNullOrWhiteSpace(item.货号))
                {
                    continue;
                }

                var key = (item.排产编号!, item.货号!);
                if (existingDict.TryGetValue(key, out var existing))
                {
                    // 更新已有记录：先对齐主键编号，再 SetValues
                    item.编号 = existing.编号;
                    _context.Entry(existing).CurrentValues.SetValues(item);
                    result.Add(existing);
                }
                else
                {
                    // 新增记录
                    if (string.IsNullOrWhiteSpace(item.编号))
                    {
                        item.编号 = Guid.NewGuid().ToString();
                    }
                    if (string.IsNullOrWhiteSpace(item.关联编号))
                    {
                        item.关联编号 = Guid.NewGuid().ToString();
                    }
                    await _context.外产_发运.AddAsync(item);
                    result.Add(item);
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        /// <summary>
        /// 根据货号查询外产发运列表
        /// </summary>
        public async Task<List<ExternalProductionShipment>> GetExternalProductionShipmentList(string? itemNo)
        {
            var query = _context.外产_发运.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(itemNo))
            {
                query = query.Where(e => e.货号 == itemNo);
            }

            return await query.ToListAsync();
        }

        /// <summary>
        /// 批量删除外产发运数据
        /// </summary>
        public async Task DeleteExternalProductionShipmentList(List<string> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                throw new ArgumentException("删除列表不能为空", nameof(ids));
            }

            var items = await _context.外产_发运
                .Where(x => ids.Contains(x.编号))
                .ToListAsync();

            if (items.Count > 0)
            {
                _context.外产_发运.RemoveRange(items);
                await _context.SaveChangesAsync();
            }
        }

        #endregion

        #region 外产领料

        /// <summary>
        /// 批量添加或更新外产领料数据（存在则覆盖，不存在则新增）
        /// </summary>
        public async Task<List<ExternalProductionPickMaterial>> AddOrUpdateExternalProductionPickMaterialList(List<ExternalProductionPickMaterial> list)
        {
            if (list == null || list.Count == 0)
            {
                throw new ArgumentException("外产领料数据不能为空", nameof(list));
            }

            var result = new List<ExternalProductionPickMaterial>();
            var keys = list
                .Where(x => !string.IsNullOrWhiteSpace(x.排产编号) && !string.IsNullOrWhiteSpace(x.货号))
                .Select(x => (x.排产编号!, x.货号!))
                .Distinct()
                .ToList();

            var schedulingNos = keys.Select(k => k.Item1).Distinct().ToList();

            // 查询对应的外产发运记录，用于获取关联编号
            var shipmentItems = await _context.外产_发运
                .AsNoTracking()
                .Where(x => schedulingNos.Contains(x.排产编号) && x.排产编号 != null)
                .Select(x => new { x.排产编号, x.关联编号 })
                .ToListAsync();

            var shipmentDict = shipmentItems
                .Where(x => x.排产编号 != null)
                .GroupBy(x => x.排产编号!)
                .ToDictionary(g => g.Key, g => g.First().关联编号);

            var existingItems = await _context.外产_领料
                .Where(x => schedulingNos.Contains(x.排产编号) && x.排产编号 != null)
                .ToListAsync();

            var existingDict = existingItems
                .Where(x => x.排产编号 != null && x.货号 != null)
                .ToDictionary(x => (x.排产编号!, x.货号!));

            foreach (var item in list)
            {
                if (string.IsNullOrWhiteSpace(item.排产编号) || string.IsNullOrWhiteSpace(item.货号))
                {
                    continue;
                }

                var key = (item.排产编号!, item.货号!);
                if (existingDict.TryGetValue(key, out var existing))
                {
                    item.编号 = existing.编号;
                    // 从外产发运获取关联编号
                    if (shipmentDict.TryGetValue(item.排产编号!, out var relNo))
                    {
                        item.关联编号 = relNo;
                    }
                    _context.Entry(existing).CurrentValues.SetValues(item);
                    result.Add(existing);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(item.编号))
                    {
                        item.编号 = Guid.NewGuid().ToString();
                    }
                    // 从外产发运获取关联编号
                    if (shipmentDict.TryGetValue(item.排产编号!, out var relNo))
                    {
                        item.关联编号 = relNo;
                    }
                    await _context.外产_领料.AddAsync(item);
                    result.Add(item);
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        /// <summary>
        /// 根据货号查询外产领料列表
        /// </summary>
        public async Task<List<ExternalProductionPickMaterial>> GetExternalProductionPickMaterialList(string? itemNo)
        {
            var query = _context.外产_领料.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(itemNo))
            {
                query = query.Where(e => e.货号 == itemNo);
            }

            return await query.ToListAsync();
        }

        /// <summary>
        /// 批量删除外产领料数据
        /// </summary>
        public async Task DeleteExternalProductionPickMaterialList(List<string> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                throw new ArgumentException("删除列表不能为空", nameof(ids));
            }

            var items = await _context.外产_领料
                .Where(x => ids.Contains(x.编号))
                .ToListAsync();

            if (items.Count > 0)
            {
                _context.外产_领料.RemoveRange(items);
                await _context.SaveChangesAsync();
            }
        }

        #endregion

        #region 外产生产

        /// <summary>
        /// 批量添加或更新外产生产数据（存在则覆盖，不存在则新增）
        /// </summary>
        public async Task<List<ExternalProduction>> AddOrUpdateExternalProductionList(List<ExternalProduction> list)
        {
            if (list == null || list.Count == 0)
            {
                throw new ArgumentException("外产生产数据不能为空", nameof(list));
            }

            var result = new List<ExternalProduction>();
            var keys = list
                .Where(x => !string.IsNullOrWhiteSpace(x.排产编号) && !string.IsNullOrWhiteSpace(x.货号))
                .Select(x => (x.排产编号!, x.货号!))
                .Distinct()
                .ToList();

            var schedulingNos = keys.Select(k => k.Item1).Distinct().ToList();

            // 查询对应的外产发运记录，用于获取关联编号
            var shipmentItems = await _context.外产_发运
                .AsNoTracking()
                .Where(x => schedulingNos.Contains(x.排产编号) && x.排产编号 != null)
                .Select(x => new { x.排产编号, x.关联编号 })
                .ToListAsync();

            var shipmentDict = shipmentItems
                .Where(x => x.排产编号 != null)
                .GroupBy(x => x.排产编号!)
                .ToDictionary(g => g.Key, g => g.First().关联编号);

            var existingItems = await _context.外产_生产
                .Where(x => schedulingNos.Contains(x.排产编号) && x.排产编号 != null)
                .ToListAsync();

            var existingDict = existingItems
                .Where(x => x.排产编号 != null && x.货号 != null)
                .ToDictionary(x => (x.排产编号!, x.货号!));

            foreach (var item in list)
            {
                if (string.IsNullOrWhiteSpace(item.排产编号) || string.IsNullOrWhiteSpace(item.货号))
                {
                    continue;
                }

                var key = (item.排产编号!, item.货号!);
                if (existingDict.TryGetValue(key, out var existing))
                {
                    item.编号 = existing.编号;
                    // 从外产发运获取关联编号
                    if (shipmentDict.TryGetValue(item.排产编号!, out var relNo))
                    {
                        item.关联编号 = relNo;
                    }
                    _context.Entry(existing).CurrentValues.SetValues(item);
                    result.Add(existing);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(item.编号))
                    {
                        item.编号 = Guid.NewGuid().ToString();
                    }
                    // 从外产发运获取关联编号
                    if (shipmentDict.TryGetValue(item.排产编号!, out var relNo))
                    {
                        item.关联编号 = relNo;
                    }
                    await _context.外产_生产.AddAsync(item);
                    result.Add(item);
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        /// <summary>
        /// 根据货号查询外产生产列表
        /// </summary>
        public async Task<List<ExternalProduction>> GetExternalProductionList(string? itemNo)
        {
            var query = _context.外产_生产.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(itemNo))
            {
                query = query.Where(e => e.货号 == itemNo);
            }

            return await query.ToListAsync();
        }

        /// <summary>
        /// 批量删除外产生产数据
        /// </summary>
        public async Task DeleteExternalProductionList(List<string> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                throw new ArgumentException("删除列表不能为空", nameof(ids));
            }

            var items = await _context.外产_生产
                .Where(x => ids.Contains(x.编号))
                .ToListAsync();

            if (items.Count > 0)
            {
                _context.外产_生产.RemoveRange(items);
                await _context.SaveChangesAsync();
            }
        }

        #endregion

        #region 外产入库

        /// <summary>
        /// 批量添加或更新外产入库数据（存在则覆盖，不存在则新增）
        /// </summary>
        public async Task<List<ExternalProductionWarehousing>> AddOrUpdateExternalProductionWarehousingList(List<ExternalProductionWarehousing> list)
        {
            if (list == null || list.Count == 0)
            {
                throw new ArgumentException("外产入库数据不能为空", nameof(list));
            }

            var result = new List<ExternalProductionWarehousing>();
            var keys = list
                .Where(x => !string.IsNullOrWhiteSpace(x.排产编号) && !string.IsNullOrWhiteSpace(x.货号))
                .Select(x => (x.排产编号!, x.货号!))
                .Distinct()
                .ToList();

            var schedulingNos = keys.Select(k => k.Item1).Distinct().ToList();

            // 查询对应的外产发运记录，用于获取关联编号
            var shipmentItems = await _context.外产_发运
                .AsNoTracking()
                .Where(x => schedulingNos.Contains(x.排产编号) && x.排产编号 != null)
                .Select(x => new { x.排产编号, x.关联编号 })
                .ToListAsync();

            var shipmentDict = shipmentItems
                .Where(x => x.排产编号 != null)
                .GroupBy(x => x.排产编号!)
                .ToDictionary(g => g.Key, g => g.First().关联编号);

            var existingItems = await _context.外产_入库
                .Where(x => schedulingNos.Contains(x.排产编号) && x.排产编号 != null)
                .ToListAsync();

            var existingDict = existingItems
                .Where(x => x.排产编号 != null && x.货号 != null)
                .ToDictionary(x => (x.排产编号!, x.货号!));

            foreach (var item in list)
            {
                if (string.IsNullOrWhiteSpace(item.排产编号) || string.IsNullOrWhiteSpace(item.货号))
                {
                    continue;
                }

                var key = (item.排产编号!, item.货号!);
                if (existingDict.TryGetValue(key, out var existing))
                {
                    item.编号 = existing.编号;
                    // 从外产发运获取关联编号
                    if (shipmentDict.TryGetValue(item.排产编号!, out var relNo))
                    {
                        item.关联编号 = relNo;
                    }
                    _context.Entry(existing).CurrentValues.SetValues(item);
                    result.Add(existing);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(item.编号))
                    {
                        item.编号 = Guid.NewGuid().ToString();
                    }
                    // 从外产发运获取关联编号
                    if (shipmentDict.TryGetValue(item.排产编号!, out var relNo))
                    {
                        item.关联编号 = relNo;
                    }
                    await _context.外产_入库.AddAsync(item);
                    result.Add(item);
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        /// <summary>
        /// 根据货号查询外产入库列表
        /// </summary>
        public async Task<List<ExternalProductionWarehousing>> GetExternalProductionWarehousingList(string? itemNo)
        {
            var query = _context.外产_入库.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(itemNo))
            {
                query = query.Where(e => e.货号 == itemNo);
            }

            return await query.ToListAsync();
        }

        /// <summary>
        /// 批量删除外产入库数据
        /// </summary>
        public async Task DeleteExternalProductionWarehousingList(List<string> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                throw new ArgumentException("删除列表不能为空", nameof(ids));
            }

            var items = await _context.外产_入库
                .Where(x => ids.Contains(x.编号))
                .ToListAsync();

            if (items.Count > 0)
            {
                _context.外产_入库.RemoveRange(items);
                await _context.SaveChangesAsync();
            }
        }

        #endregion


        // 获取合同状态
        public async Task<PMCBasicInfo> GetContractStatus(string num)
        {
            if (string.IsNullOrEmpty(num))
            {
                Console.WriteLine("合同号不能为空。");
                return new PMCBasicInfo();
            }

            PMCBasicInfo contract = new();
            List<PMCBasicInfo> contracts = await _context.外销合同基本信息
                 .AsNoTracking()
                 .Where(e => e.合同号 == num).ToListAsync();


            if (contracts.Count > 0)
            {
                contract = contracts[0];
                Console.WriteLine($"合同号: {contract.合同号}, 合同状态: {contract.合同状态}");
            }
            else
            {
                Console.WriteLine("未找到对应的合同信息。");
            }
            return contract;
        }




        // 获取产品资料装配清单（优化版：消除递归）
        public async Task<List<ProductDataAssemblyList>> GetProductDataAssemblyList(string? itemNo)
        {
            if (string.IsNullOrWhiteSpace(itemNo))
            {
                return new List<ProductDataAssemblyList>();
            }

            // 使用 HashSet 记录已访问的货号，防止无限递归
            HashSet<string> visitedItemNos = new HashSet<string>();
            return await GetProductDataAssemblyListRecursive(itemNo, visitedItemNos);
        }

        /// <summary>
        /// 递归获取产品资料装配清单
        /// </summary>
        private async Task<List<ProductDataAssemblyList>> GetProductDataAssemblyListRecursive(string itemNo, HashSet<string> visitedItemNos)
        {
            // 预处理：如果 itemNo 包含括号，取第一个括号之前的内容
            string processedItemNo = itemNo;
            int bracketIndex = itemNo.IndexOf('(');
            bool hadBracket = bracketIndex > 0;
            if (hadBracket)
            {
                processedItemNo = itemNo.Substring(0, bracketIndex);
            }

            // 检查是否已访问过此货号，防止无限递归
            if (!visitedItemNos.Add(processedItemNo))
            {
                return new List<ProductDataAssemblyList>();
            }

            // 使用处理后的 itemNo 获取产品资料装配信息
            ProductDataAssembly? productData = await _context.产品资料装配
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.货号 == processedItemNo);

            if (productData == null)
            {
                return new List<ProductDataAssemblyList>();
            }

            List<ProductDataAssemblyList> productDataList = await _context.产品资料装配清单
                .AsNoTracking()
                .Where(e => e.主编号 == productData.编号)
                .ToListAsync();

            // 如果查询结果只有一条，且当前使用的 itemNo 是经过预处理的，则继续递归
            if (productDataList.Count == 1 && hadBracket)
            {
                string? nextItemNo = productDataList[0].货号;
                if (!string.IsNullOrWhiteSpace(nextItemNo))
                {
                    return await GetProductDataAssemblyListRecursive(nextItemNo, visitedItemNos);
                }
            }

            return productDataList;
        }

        // 获取产品资料装配信息
        public async Task<ProductDataAssembly> GetProductDataAssembly(string itemNo)
        {

            var productData = await _context.产品资料装配
                   .AsNoTracking()
                   .FirstOrDefaultAsync(e => e.货号 == itemNo);
            return productData;
        }

        // 获取产品资料
        public async Task<ProductData?> GetProductData(string? itemNo)
        {
            if (string.IsNullOrWhiteSpace(itemNo))
            {
                return null;
            }
            var productData = await _context.产品资料
                   .AsNoTracking()
                   .FirstOrDefaultAsync(e => e.货号 == itemNo);
            return productData;
        }

        // 校验线圈货号
        public async Task<bool> SearchCoils(string? keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return false;
            }
            var query = _context.产品资料.AsNoTracking()
                .Where(p => (!string.IsNullOrEmpty(p.产品类别) && p.产品类别.StartsWith("线圈")) && p.停用 != "1")
                .Where(p => p.货号 == keyword);
            if (!await query.AnyAsync())
            {
                return false;
            }
            return true;
        }




        // 定义一个简单的内部结构体，避免频繁解析字符串
        private class IntermediateData
        {
            public string? 合同号 { get; set; }
            public string? 排产编号 { get; set; }
            public string? 货号 { get; set; }
            public string? 父级货号 { get; set; }
            public string? 物料货号 { get; set; }
            public string? 中文品名 { get; set; }
            public string? 中文规格 { get; set; }
            public string? 分析单号 { get; set; }
            public string? 商品属性 { get; set; }
            public string? 关联编号 { get; set; }
            //在产数
            public int 在产数 { get; set; }
            //需求量
            public int 需求量 { get; set; }
            //发运数量
            public int 发运数量 { get; set; }
            //仓库数
            public int 仓库数 { get; set; }
            public string? 排产用户 { get; set; }
            public List<DeliveryPlan>? 交货计划 { get; set; }
        }
    }
}