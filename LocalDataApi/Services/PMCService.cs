using LocalDataApi.Data;
using LocalDataApi.Dto;
using LocalDataApi.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace LocalDataApi.Services
{
    public class PMCService
    {
        private readonly AppDbContext _context;
        public PMCService(AppDbContext context)
        {
            _context = context;
        }
        // 获取PMC产品列表信息
        public async Task<List<PMCProductInfo>> GetPMCProductListInfo(PMCRequestDto request)
        {
            var query = _context.外销合同产品
                .AsNoTracking()
                .Where(e => e.分析单号 == "PCZY126030646" && e.层 == "0")
                .AsQueryable();
            //.Where(e => !string.IsNullOrEmpty(e.合同号) && e.层 == "0")
            /* List<PMCProductInfo> productList = await _context.外销合同产品
                     .AsNoTracking()
                     .Where(e => !string.IsNullOrEmpty(e.合同号)
                     //&& GetContractStatus(e.合同号).Result.合同状态!= "完成" 
                     //&& !string.IsNullOrEmpty(e.分析单号)
                     && e.层 == "0").ToListAsync();
             //productList= productList.Where(e =>!string.IsNullOrEmpty(GetContractStatus(e.合同号).Result.合同状态) && GetContractStatus(e.合同号).Result.合同状态 != "完成").ToList();
            */
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
            var existing = await _context.信息交期评审
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
                await _context.信息交期评审.AddAsync(deliveryReview);
            }

            // 保存到数据库
            await _context.SaveChangesAsync();

            // 返回更新或新增后的实体（如果是更新，返回 existing 更准确）
            return existing ?? deliveryReview;
        }

        //PMC交期评审列表
        public async Task<List<PMCDeliveryReview>> GetPMCDeliveryReviewList()
        {
            var query = _context.信息交期评审
               .AsNoTracking()
               .AsQueryable();
            var data = await query.ToListAsync();
            return data;
        }

        //查询PMC产品销控表
        public async Task<List<PMCSalesControl>> GetPMCSalesControlList(string number)
        {
            return await _context.产品销控表
               .Where(e => e.货号 == number)
               .AsNoTracking()
               .ToListAsync();
        }

        //添加PMC产品销控列表
        public async Task<List<PMCSalesControl>> AddPMCSalesControlList()
        {
            // 1. 加载评审数据
            var reviewList = await _context.信息交期评审
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
                    var allPlans = g.SelectMany(x => x.Plans).ToList();

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
                var existingDict = await _context.产品销控表
                    .Where(x => result.Select(r => r.货号).Contains(x.货号))
                    .ToDictionaryAsync(x => x.货号);

                foreach (var newItem in result)
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

        //保存PMC产品信息
        public async Task<List<PMCProductInfo>> SavePMCProductInfoAsync(PMCDeliveryReview deliveryReview)
        {
            List<PMCProductInfo> pmcProductInfos = new();
            var dataAssemblyLists = await GetProductDataAssemblyList(deliveryReview.物料货号); // 获取产品资料装配清单（如果需要）    

            foreach (var productData in dataAssemblyLists)
            {
                var data = await GetProductData(productData.货号);

                var productInfo = new PMCProductInfo
                {
                    分析单号 = deliveryReview.分析单号,
                    货号 = deliveryReview.货号,
                    中文品名 = data.中文品名,
                    中文规格 = data.中文规格,
                };
                pmcProductInfos.Add(productInfo);
            }

            await _context.外销合同产品.AddRangeAsync(pmcProductInfos);
            return pmcProductInfos;
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
        public async Task<List<SchedulingAnalysisDto>> GetSchedulingAnalysisListDto(PMCRequestDto request)
        {
            List<SchedulingAnalysisDto> anaylsisDtos = new();

            PMCSalesControl salesData = (await GetPMCSalesControlList(request.货号)).FirstOrDefault();
            List<ProductDataAssemblyList> productDataAssemblyList = await GetProductDataAssemblyList(request.货号);

            //string rootItemNo = "";
            //if (salesData != null)
            //{
            //    rootItemNo = salesData.父级货号 == request.货号 ? request.货号 : salesData.父级货号;
            //}
            //根据货号获取品名等相关信息
            var productRootData = await GetProductData(salesData.父级货号);

            var productSelfData = await GetProductData(salesData.货号);

            //var rootData = new SchedulingAnalysisDto
            //{
            //    合同号= salesData.合同号,
            //    货号 = salesData.父级货号,
            //    层 = "0",
            //    产品属性 = "",
            //    品名 = productRootData.中文品名,
            //    规格 = productRootData.中文规格,               
            //};

            var selfData = new SchedulingAnalysisDto
            {
                货号 = request.货号,
                层 = "0",
                产品属性 = "",
                品名 = productSelfData.中文品名,
                规格 = productSelfData.中文规格,
            };

            //anaylsisDtos.Add(rootData);
            anaylsisDtos.Add(selfData);
            foreach (var dataAssembly in productDataAssemblyList)
            {
                var productChildData = await GetProductData(dataAssembly.货号);
                var childrenData = new SchedulingAnalysisDto
                {
                    货号 = dataAssembly.货号,
                    层 = "1",
                    产品属性 = "",
                    品名 = productChildData.中文品名,
                    规格 = productChildData.中文规格,
                    来源 = dataAssembly.来源,
                    用量 = dataAssembly.用量,
                    单位 = dataAssembly.单位,
                };
                anaylsisDtos.Add(childrenData);
            }
            return anaylsisDtos;
        }


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

        // 获取产品资料装配清单
        public async Task<List<ProductDataAssemblyList>> GetProductDataAssemblyList(string itemNo)
        {
            // 1. 预处理：如果 itemNo 包含括号，取第一个括号之前的内容
            string processedItemNo = itemNo;
            int bracketIndex = itemNo.IndexOf('(');
            if (bracketIndex > 0)
            {
                processedItemNo = itemNo.Substring(0, bracketIndex);
            }

            // 2. 使用处理后的 itemNo 获取产品资料装配信息
            ProductDataAssembly productData = await GetProductDataAssembly(processedItemNo);
            List<ProductDataAssemblyList> productDataList = await _context.产品资料装配清单
                 .AsNoTracking()
                 .Where(e => e.主编号 == productData.编号).ToListAsync();

            // 3. 如果查询结果只有一条，且当前使用的 itemNo 是经过预处理的（即原始 itemNo 含括号）
            //    则尝试从这条记录中提取新的货号再次查询
            if (productDataList.Count == 1 && bracketIndex > 0)
            {
                // 假设 ProductDataAssemblyList 实体中包含一个名为 "货号" 的属性
                string newItemNo = productDataList[0].货号;  // 请根据实际字段名修改

                // 递归调用自身,按照需求，应该是使用新货号再次查询
                return await GetProductDataAssemblyList(newItemNo);
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
        public async Task<ProductData> GetProductData(string itemNo)
        {
            var productData = await _context.产品资料
                   .AsNoTracking()
                   .FirstOrDefaultAsync(e => e.货号 == itemNo);
            return productData;
        }

        // 校验线圈货号
        public async Task<bool> SearchCoils(string keyword)
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
            public string 合同号 { get; set; }
            public string 排产编号 { get; set; }
            public string 货号 { get; set; }
            public string 父级货号 { get; set; }
            public string 物料货号 { get; set; }
            public string 中文品名 { get; set; }
            public string 中文规格 { get; set; }
            public string 分析单号 { get; set; }
            public string 商品属性 { get; set; }
            public int InProd { get; set; }
            public int Demand { get; set; }
            public int Warehouse { get; set; }
            public List<DeliveryPlan> Plans { get; set; }
        }
    }
}
