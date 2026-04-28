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




        // 添加PMC交期评审信息
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
                var analysisNum = await GenerateAnalysisOrderNumberAsync(deliveryReview.排产用户);

                // 创建排产分析单并保存到数据库
                var scheduling = await SaveSchedulingAnalysisAsync(deliveryReview, analysisNum);

                deliveryReview.编号 = scheduling.编号; // 使用排产分析单的编号作为交期评审的编号
                deliveryReview.分析单号 = analysisNum;
                // 新增
                await _context.外产_订单.AddAsync(deliveryReview);
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

            reviewList.ForEach(e => e.分析单号 = "PCZY126030646");

            // 2. 准备过滤条件
            var analysisNos = reviewList.Select(r => r.分析单号).Distinct().ToList();
            var allItemNos = reviewList.Select(r => r.货号)
                .Concat(reviewList.Select(r => r.物料货号))
                .Where(h => !string.IsNullOrEmpty(h))
                .Distinct()
                .ToList();

            // 3. 查询产品和仓库数据
            var productTask = await _context.外销合同产品
                .Where(p => analysisNos.Contains(p.分析单号) && allItemNos.Contains(p.货号))
                .AsNoTracking()
                .Select(p => new PMCProductInfo
                {
                    分析单号 = p.分析单号,
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
                .Where(p => p.分析单号 != null && p.货号 != null)
                .GroupBy(p => (p.分析单号!, p.货号!))
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
                if (!string.IsNullOrEmpty(review.物料货号) && review.物料货号 != review.货号)
                {
                    intermediateRecords.Add(CreateIntermediate(review, review.物料货号, "半成品", productDict, warehouseDict));
                }
            }

            // 6. 按货号分组合并
            var result = intermediateRecords
                .GroupBy(r => r.货号)
                .Select(g =>
                {
                    var first = g.First();

                    // 收集所有交货计划（尚未合并）
                    var allPlans = g.SelectMany(x => x.Plans ?? new List<DeliveryPlan>()).ToList();

                    // ========= 关键修改：按交货日期合并计划 =========
                    var aggregatedPlans = allPlans
                        .GroupBy(p => p.交货日期)
                        .Select(grp => new
                        {
                            交货日期 = grp.Key,
                            交货数量 = grp.Sum(p => int.Parse(p.交货数量)).ToString(),
                            状态 = ""  // 状态由前端计算，后端留空
                        })
                        .ToList();

                    // 序列化为 JSON 字符串
                    string deliveryPlanJson = JsonConvert.SerializeObject(aggregatedPlans);

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
                        在产数 = g.Sum(x => x.InProd).ToString(),
                        订单总需求 = g.Sum(x => x.Demand).ToString(),
                        仓库数 = g.Sum(x => x.Warehouse).ToString(),
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
            Dictionary<(string, string), List<PMCProductInfo>> dict, // dynamic 在这里对应匿名对象
            Dictionary<string, int> houseDict)
        {
            var key = (review.分析单号, itemNo);
            int totalDemand = 0;
            int totalInProd = 0;
            var plans = new List<DeliveryPlan>();

            if (dict.TryGetValue(key, out var products))
            {
                foreach (var p in products)
                {
                    int q = ParseInt(p.数量);
                    int s = ParseInt(p.发运数量);
                    totalDemand += (q - s);
                    totalInProd += ParseInt(p.在产需求量);
                    plans.Add(new DeliveryPlan { 交货日期 = review.交货日期, 交货数量 = p.数量 });
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
                InProd = totalInProd,
                Demand = totalDemand,
                Warehouse = houseQty,
                Plans = plans
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
            //await _context.排产分析单.AddAsync(scheduling);
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



        #region 排产分析详情列表

        // 根据销控表中的货号获取排产分析详情列表
        public async Task<List<SchedulingAnalysisDto>> GetSchedulingAnalysisListDto(PMCRequestDto request)
        {
            var result = new List<SchedulingAnalysisDto>();

            // 基础数据
            var salesControlList = await GetPMCSalesControlList(request.货号);
            var productDataAssemblyList = await GetProductDataAssemblyList(request.货号);
            var salesData = salesControlList?.FirstOrDefault();

            string schedulingNo = salesData?.排产编号;

            // 收集所有货号（父 + 子）
            var itemNos = new HashSet<string>();
            if (!string.IsNullOrEmpty(request.货号))
                itemNos.Add(request.货号);

            foreach (var item in productDataAssemblyList)
            {
                if (!string.IsNullOrEmpty(item.货号))
                    itemNos.Add(item.货号);
            }

            // 批量查询
            var productDataDict = await GetProductDataBatchAsync(itemNos.ToList());
            var warehouseGoodsDict = await GetWarehouseGoodsBatchAsync(itemNos.ToList());
            var productionDemandDict = await GetProductionDemandBatchAsync(itemNos.ToList(), schedulingNo);
            var inTransitQuantityDict = await GetInTransitQuantityBatchAsync(itemNos.ToList(), schedulingNo);

            int level = 0;

            //  统一构建方法
            SchedulingAnalysisDto BuildDto(
                string itemNo,
                int level,
                ProductDataAssemblyList? assembly = null,
                PMCSalesControl? sales = null)
            {
                productDataDict.TryGetValue(itemNo, out var productData);
                warehouseGoodsDict.TryGetValue(itemNo, out var goodsData);
                productionDemandDict.TryGetValue(itemNo, out var productionDemand);
                inTransitQuantityDict.TryGetValue(itemNo, out var inTransit);

                return new SchedulingAnalysisDto
                {
                    货号 = itemNo,
                    层 = level.ToString(),

                    品名 = sales?.中文品名 ?? productData?.中文品名,
                    规格 = sales?.中文规格 ?? productData?.中文规格,

                    来源 = assembly?.来源 ?? "",
                    用量 = assembly?.用量 ?? "",
                    单位 = assembly?.单位 ?? "",

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

            // 父级（作为第一个节点）
            result.Add(BuildDto(request.货号, level, null, salesData));

            // 子级（复用同一方法）
            foreach (var assembly in productDataAssemblyList)
            {
                result.Add(BuildDto(assembly.货号, level + 1, assembly));
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
        private async Task<Dictionary<string, WarehouseGoods>> GetWarehouseGoodsBatchAsync(List<string> itemNos)
        {
            if (itemNos == null || itemNos.Count == 0)
            {
                return new Dictionary<string, WarehouseGoods>();
            }

            var warehouseGoodsList = await _context.仓库货品
                .AsNoTracking()
                .Where(e => itemNos.Contains(e.货号) && !string.IsNullOrEmpty(e.货号))
                .ToListAsync();

            // 使用 GroupBy + First() 处理重复货号，保持与 GetWarehouseGoodsByItemNo 行为一致
            return warehouseGoodsList.Where(e => e.货号 != null)
                .GroupBy(e => e.货号!)
                .ToDictionary(g => g.Key, g => g.First());
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
            var query = _context.工单管理
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
            var existing = await _context.工单管理
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
            // 开始计时
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
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
                var existing = await _context.工单管理
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
                        成品品名 = workOrder.成品品名,
                        规格 = workOrder.规格,
                    };
                    await _context.工单管理.AddAsync(newWorkOrder);
                }

                // 保存到数据库
                await _context.SaveChangesAsync();

                // 返回更新或新增后的实体
                return workOrder;
            }
            finally
            {
                // 停止计时并记录执行时间
                stopwatch.Stop();
                Console.WriteLine($"AddPMCWorkOrder 方法执行时间: {stopwatch.ElapsedMilliseconds} 毫秒");
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

            string currentItemNo = itemNo;
            List<ProductDataAssemblyList> result = new List<ProductDataAssemblyList>();

            // 最多递归3层，防止无限循环
            for (int depth = 0; depth < 3; depth++)
            {
                // 预处理：如果 itemNo 包含括号，取第一个括号之前的内容
                string processedItemNo = currentItemNo;
                int bracketIndex = currentItemNo.IndexOf('(');
                bool hadBracket = bracketIndex > 0;
                if (hadBracket)
                {
                    processedItemNo = currentItemNo.Substring(0, bracketIndex);
                }

                // 使用处理后的 itemNo 获取产品资料装配信息
                ProductDataAssembly? productData = await _context.产品资料装配
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.货号 == processedItemNo);

                if (productData == null)
                {
                    break; // 找不到装配信息，终止循环
                }

                List<ProductDataAssemblyList> productDataList = await _context.产品资料装配清单
                    .AsNoTracking()
                    .Where(e => e.主编号 == productData.编号)
                    .ToListAsync();

                // 如果查询结果只有一条，且当前使用的 itemNo 是经过预处理的
                if (productDataList.Count == 1 && hadBracket)
                {
                    currentItemNo = productDataList[0].货号 ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(currentItemNo))
                    {
                        break;
                    }
                    // 继续下一轮循环，使用新货号查询
                    continue;
                }

                // 添加到结果并退出循环
                result.AddRange(productDataList);
                break;
            }

            return result;
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
            //在产数
            public int InProd { get; set; }
            //需求量
            public int Demand { get; set; }
            //仓库数
            public int Warehouse { get; set; }
            public List<DeliveryPlan>? Plans { get; set; }
        }
    }
}