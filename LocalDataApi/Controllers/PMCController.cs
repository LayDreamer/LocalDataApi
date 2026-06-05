using LocalDataApi.Dto;
using LocalDataApi.Exceptions;
using LocalDataApi.Models;
using LocalDataApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace LocalDataApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PMCController : ControllerBase
    {
        private readonly IPMCService _pmcService;

        public PMCController(IPMCService pmcService)
        {
            _pmcService = pmcService;
        }

        /// <summary>
        /// 转换交期评审列表(根据外销合同客户产品表)
        /// </summary>
        [HttpPost("ConvertToPMCDeliveryReviewList")]
        public async Task<ActionResult<ApiResponse<List<PMCDeliveryReview>>>>
            ConvertToPMCDeliveryReviewList(PMCRequestDto requestDto)
        {
            try
            {
                var userProductList = await _pmcService.ConvertToPMCDeliveryReviewList(requestDto);
                return Ok(new ApiResponse<List<PMCDeliveryReview>>()
                {
                    Success = true,
                    Message = "获取成功！",
                    Data = userProductList // 空列表也正常返回
                });
            }
            catch (Exception e)
            {
               return BadRequest($"错误提示：{e.Message}");
            }
        }


        /// <summary>
        /// 获取产品信息列表
        /// </summary>
        [HttpPost("ProductListInfo")]
        public async Task<ActionResult<ApiResponse<object>>> GetPMCProductInfo(PMCRequestDto requestDto)
        {
            var basicInfo = await _pmcService.GetPMCProductListInfo(requestDto);
            // 无论是否有数据，都视为查询成功
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "查询成功！",
                Data = basicInfo
            });
        }

        /// <summary>
        /// 根据货号获取产品资料装配清单
        /// </summary>
        [HttpPost("ProductDataAssemblyList")]
        public async Task<ActionResult<ApiResponse<object>>> GetProductDataAssemblyList(PMCRequestDto requestDto)
        {
            if (string.IsNullOrWhiteSpace(requestDto.货号))
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "货号不能为空！"
                });
            }

            try
            {
                var result = await _pmcService.GetProductDataAssemblyList(requestDto.货号);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "查询成功！",
                    Data = result
                });
            }
            catch (InvalidOperationException)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"未找到货号 {requestDto.货号} 对应的产品资料装配信息"
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "服务器内部错误"
                });
            }
        }

        /// <summary>
        /// 检查线圈货号是否存在于装配清单中
        /// </summary>
        [HttpPost("CheckAssemblyList")]
        public async Task<ActionResult<ApiResponse<object>>> CheckIsExistInAssemblyList(PMCRequestDto requestDto)
        {
            if (string.IsNullOrWhiteSpace(requestDto.线圈货号))
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "线圈货号不能为空！"
                });
            }

            var result = await _pmcService.SearchCoils(requestDto.线圈货号);
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "查询成功！",
                Data = result
            });
        }

        /// <summary>
        /// 获取交期评审列表
        /// </summary>
        [HttpPost("PMCDeliveryReviewList")]
        public async Task<ActionResult<ApiResponse<object>>> GetPMCDeliveryReviewList(PMCRequestDto requestDto)
        {
            try
            {
                var reviewList = await _pmcService.GetPMCDeliveryReviewList(requestDto);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "获取成功！",
                    Data = reviewList // 空列表也正常返回
                });
            }
            catch (Exception e)
            {
                return BadRequest($"错误提示：{e.Message}");
            }
        }

        /// <summary>
        /// 新增交期评审记录
        /// </summary>
        [HttpPost("AddPMCDeliveryReview")]
        public async Task<ActionResult<ApiResponse<object>>> AddPMCDeliveryReview(PMCDeliveryReview review)
        {
            try
            {
                var result = await _pmcService.AddPMCDeliveryReview(review);
                if (result == null)
                {
                    return Ok(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "新增失败，未返回有效数据！"
                    });
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "新增成功！",
                    Data = result
                });
            }
            catch (Exception e)
            {
              return BadRequest($"错误提示：{e.Message}");

            }
        }


        /// <summary>
        /// 
        /// </summary>
        [HttpPost("AddPMCSalesControlList")]
        public async Task<ActionResult<ApiResponse<object>>> AddPMCSalesControlList()
        {
            try
            {
                var reviewList = await _pmcService.AddPMCSalesControlList();
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "获取成功！",
                    Data = reviewList // 空列表也正常返回
                });
            }
            catch (Exception e)
            {
                return BadRequest($"错误提示：{e.Message}");
            }
        }

        [HttpPost("GetPMCSalesControlList")]
        public async Task<ActionResult<ApiResponse<object>>> GetPMCSalesControlList(PMCRequestDto requestDto)
        {
            if (string.IsNullOrWhiteSpace(requestDto.货号))
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "货号不能为空！"
                });
            }

            try
            {
                var reviewList = await _pmcService.GetPMCSalesControlList(requestDto.货号);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "获取成功！",
                    Data = reviewList // 空列表也正常返回
                });
            }
            catch (ServiceException e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "服务器内部错误"
                });
            }
        }
        [HttpPost("GetPMCProductData")]
        public async Task<ActionResult<ApiResponse<object>>> GetPMCProductData(PMCRequestDto requestDto)
        {
            if (string.IsNullOrWhiteSpace(requestDto.货号))
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "货号不能为空！"
                });
            }

            try
            {
                var productData = await _pmcService.GetProductData(requestDto.货号);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "获取成功！",
                    Data = productData // 空列表也正常返回
                });
            }
            catch (ServiceException e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "服务器内部错误"
                });
            }
        }

        [HttpPost("SchedulingAnalysisList")]
        public async Task<ActionResult<ApiResponse<object>>> GetSchedulingAnalysisList(PMCRequestDto requestDto)
        {
            try
            {
                var productData = await _pmcService.GetSchedulingAnalysisListDto(requestDto);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "获取成功！",
                    Data = productData // 空列表也正常返回
                });
            }
            catch (Exception e)
            {
                return BadRequest($"错误提示：{e.Message}");
            }
        }

        /// <summary>
        /// 获取全部工单列表
        /// </summary>
        [HttpPost("GetPMCWorkOrderList")]
        public async Task<ActionResult<ApiResponse<object>>> GetPMCWorkOrderList()
        {
            try
            {
                var workOrderList = await _pmcService.GetPMCWorkOrderList();
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "获取成功！",
                    Data = workOrderList // 空列表也正常返回
                });
            }
            catch (Exception e)
            {
               return BadRequest($"错误提示：{e.Message}");
            }
        }

        /// <summary>
        /// 更新工单
        /// </summary>
        [HttpPost("UpdatePMCWorkOrder")]
        public async Task<ActionResult<ApiResponse<object>>> UpdatePMCWorkOrder(PMCWorkOrder workOrder)
        {
            try
            {
                var productData = await _pmcService.UpdatePMCWorkOrder(workOrder);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "更新成功！",
                    Data = productData
                });
            }
            catch (Exception e)
            {
                return BadRequest($"错误提示：{e.Message}");
            }
        }

        [HttpPost("AddPMCWorkOrder")]
        public async Task<ActionResult<ApiResponse<object>>> AddPMCWorkOrder(PMCWorkOrder workOrder)
        {
            try
            {
                var productData = await _pmcService.AddPMCWorkOrder(workOrder);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "添加成功！",
                    Data = productData // 空列表也正常返回
                });
            }
            catch (Exception e)
            {
                return BadRequest($"错误提示：{e.Message}");
            }
        }

        /// <summary>
        /// 根据货号创建工单管理（从外产_订单表中查找评审通过的数据）
        /// </summary>
        [HttpPost("AddPMCWorkOrderByRequest")]
        public async Task<ActionResult<ApiResponse<object>>> AddPMCWorkOrder(PMCRequestDto requestDto)
        {
            try
            {
                var workOrder = await _pmcService.AddPMCWorkOrder(requestDto);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "创建成功！",
                    Data = workOrder
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
        }

        /// <summary>
        /// 批量添加或更新工单销控表数据（存在则覆盖，不存在则新增）
        /// </summary>
        [HttpPost("AddOrUpdateWorkOrderSalesControlList")]
        public async Task<ActionResult<ApiResponse<object>>> AddOrUpdateWorkOrderSalesControlList(List<WorkOrderSalesControl> list)
        {
            try
            {
                var result = await _pmcService.AddOrUpdateWorkOrderSalesControlList(list);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "批量保存成功！",
                    Data = result
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
        }

        /// <summary>
        /// 获取工单销控表列表
        /// </summary>
        [HttpPost("GetWorkOrderSalesControlList")]
        public async Task<ActionResult<ApiResponse<object>>> GetWorkOrderSalesControlList(PMCRequestDto requestDto)
        {
            try
            {
                var result = await _pmcService.GetWorkOrderSalesControlList(requestDto.货号);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "查询成功！",
                    Data = result
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
        }

        /// <summary>
        /// 批量添加或更新工单销控表明细数据（存在则覆盖，不存在则新增）
        /// </summary>
        [HttpPost("AddOrUpdateWorkOrderSalesControlDetailList")]
        public async Task<ActionResult<ApiResponse<object>>> AddOrUpdateWorkOrderSalesControlDetailList(List<WorkOrderSalesControlDetail> list)
        {
            try
            {
                var result = await _pmcService.AddOrUpdateWorkOrderSalesControlDetailList(list);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "批量保存成功！",
                    Data = result
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
        }

        /// <summary>
        /// 获取工单销控表明细列表
        /// </summary>
        [HttpPost("GetWorkOrderSalesControlDetailList")]
        public async Task<ActionResult<ApiResponse<object>>> GetWorkOrderSalesControlDetailList(PMCRequestDto requestDto)
        {
            try
            {
                var result = await _pmcService.GetWorkOrderSalesControlDetailList(requestDto.货号);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "查询成功！",
                    Data = result
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
        }

        /// <summary>
        /// 批量删除工单销控表明细数据
        /// </summary>
        [HttpPost("DeleteWorkOrderSalesControlDetailList")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteWorkOrderSalesControlDetailList(List<string> ids)
        {
            try
            {
                await _pmcService.DeleteWorkOrderSalesControlDetailList(ids);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "删除成功！"
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
        }

        /// <summary>
        /// 批量添加或更新外产发运数据（存在则覆盖，不存在则新增）
        /// </summary>
        [HttpPost("AddOrUpdateExternalProductionShipmentList")]
        public async Task<ActionResult<ApiResponse<object>>> AddOrUpdateExternalProductionShipmentList(List<ExternalProductionShipment> list)
        {
            try
            {
                var result = await _pmcService.AddOrUpdateExternalProductionShipmentList(list);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "批量保存成功！",
                    Data = result
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
        }

        /// <summary>
        /// 批量添加或更新外产领料数据（存在则覆盖，不存在则新增）
        /// </summary>
        [HttpPost("AddOrUpdateExternalProductionPickMaterialList")]
        public async Task<ActionResult<ApiResponse<object>>> AddOrUpdateExternalProductionPickMaterialList(List<ExternalProductionPickMaterial> list)
        {
            try
            {
                var result = await _pmcService.AddOrUpdateExternalProductionPickMaterialList(list);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "批量保存成功！",
                    Data = result
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
        }

        /// <summary>
        /// 批量添加或更新外产生产数据（存在则覆盖，不存在则新增）
        /// </summary>
        [HttpPost("AddOrUpdateExternalProductionList")]
        public async Task<ActionResult<ApiResponse<object>>> AddOrUpdateExternalProductionList(List<ExternalProduction> list)
        {
            try
            {
                var result = await _pmcService.AddOrUpdateExternalProductionList(list);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "批量保存成功！",
                    Data = result
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
        }

        /// <summary>
        /// 批量添加或更新外产入库数据（存在则覆盖，不存在则新增）
        /// </summary>
        [HttpPost("AddOrUpdateExternalProductionWarehousingList")]
        public async Task<ActionResult<ApiResponse<object>>> AddOrUpdateExternalProductionWarehousingList(List<ExternalProductionWarehousing> list)
        {
            try
            {
                var result = await _pmcService.AddOrUpdateExternalProductionWarehousingList(list);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "批量保存成功！",
                    Data = result
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
        }

        #region 外产发运查询与删除

        /// <summary>
        /// 获取外产发运列表
        /// </summary>
        [HttpPost("GetExternalProductionShipmentList")]
        public async Task<ActionResult<ApiResponse<object>>> GetExternalProductionShipmentList(PMCRequestDto requestDto)
        {
            try
            {
                var result = await _pmcService.GetExternalProductionShipmentList(requestDto.货号);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "查询成功！",
                    Data = result
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
        }

        /// <summary>
        /// 批量删除外产发运数据
        /// </summary>
        [HttpPost("DeleteExternalProductionShipmentList")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteExternalProductionShipmentList(List<string> ids)
        {
            try
            {
                await _pmcService.DeleteExternalProductionShipmentList(ids);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "删除成功！"
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
        }

        #endregion

        #region 外产领料查询与删除

        /// <summary>
        /// 获取外产领料列表
        /// </summary>
        [HttpPost("GetExternalProductionPickMaterialList")]
        public async Task<ActionResult<ApiResponse<object>>> GetExternalProductionPickMaterialList(PMCRequestDto requestDto)
        {
            try
            {
                var result = await _pmcService.GetExternalProductionPickMaterialList(requestDto.货号);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "查询成功！",
                    Data = result
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
        }

        /// <summary>
        /// 批量删除外产领料数据
        /// </summary>
        [HttpPost("DeleteExternalProductionPickMaterialList")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteExternalProductionPickMaterialList(List<string> ids)
        {
            try
            {
                await _pmcService.DeleteExternalProductionPickMaterialList(ids);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "删除成功！"
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
        }

        #endregion

        #region 外产生产查询与删除

        /// <summary>
        /// 获取外产生产列表
        /// </summary>
        [HttpPost("GetExternalProductionList")]
        public async Task<ActionResult<ApiResponse<object>>> GetExternalProductionList(PMCRequestDto requestDto)
        {
            try
            {
                var result = await _pmcService.GetExternalProductionList(requestDto.货号);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "查询成功！",
                    Data = result
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
        }

        /// <summary>
        /// 批量删除外产生产数据
        /// </summary>
        [HttpPost("DeleteExternalProductionList")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteExternalProductionList(List<string> ids)
        {
            try
            {
                await _pmcService.DeleteExternalProductionList(ids);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "删除成功！"
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
        }

        #endregion

        #region 外产入库查询与删除

        /// <summary>
        /// 获取外产入库列表
        /// </summary>
        [HttpPost("GetExternalProductionWarehousingList")]
        public async Task<ActionResult<ApiResponse<object>>> GetExternalProductionWarehousingList(PMCRequestDto requestDto)
        {
            try
            {
                var result = await _pmcService.GetExternalProductionWarehousingList(requestDto.货号);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "查询成功！",
                    Data = result
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
        }

        /// <summary>
        /// 批量删除外产入库数据
        /// </summary>
        [HttpPost("DeleteExternalProductionWarehousingList")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteExternalProductionWarehousingList(List<string> ids)
        {
            try
            {
                await _pmcService.DeleteExternalProductionWarehousingList(ids);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "删除成功！"
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
        }

        #endregion
    }
}