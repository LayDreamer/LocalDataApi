using LocalDataApi.Dto;
using LocalDataApi.Exceptions;
using LocalDataApi.Models;
using LocalDataApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LocalDataApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("DatabaseHeavy")]
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
        public async Task<ActionResult<ApiResponse<PagedResult<PMCDeliveryReview>>>>
            ConvertToPMCDeliveryReviewList(PMCRequestDto requestDto)
        {
            try
            {
                var userProductList = await _pmcService.ConvertToPMCDeliveryReviewList(requestDto, HttpContext.RequestAborted);
                return Ok(new ApiResponse<PagedResult<PMCDeliveryReview>>()
                {
                    Success = true,
                    Message = "获取成功！",
                    Data = userProductList // 空列表也正常返回
                });
            }
            catch (Exception)
            {
                throw;
            }
        }


        /// <summary>
        /// 获取产品信息列表
        /// </summary>
        [HttpPost("ProductListInfo")]
        public async Task<ActionResult<ApiResponse<PagedResult<PMCProductInfo>>>> GetPMCProductInfo(PMCRequestDto requestDto)
        {
            var basicInfo = await _pmcService.GetPMCProductListInfo(requestDto, HttpContext.RequestAborted);
            // 无论是否有数据，都视为查询成功
            return Ok(new ApiResponse<PagedResult<PMCProductInfo>>
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
        /// 根据货号获取产品资料装配清单中中间件等于 0 的记录
        /// </summary>
        [HttpPost("ProductDataAssemblyListByItemNo")]
        public async Task<ActionResult<ApiResponse<object>>> GetProductDataAssemblyListByItemNo(PMCRequestDto requestDto)
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
                var result = await _pmcService.GetProductDataAssemblyListByItemNo(requestDto.货号);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "查询成功！",
                    Data = result
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
        /// 按关键字模糊查询产品资料中的线圈（货号包含关键字即可），最多返回 50 条
        /// </summary>
        [HttpPost("SearchCoilsByKeyword")]
        public async Task<ActionResult<ApiResponse<object>>> SearchCoilsByKeyword(PMCRequestDto requestDto)
        {
            if (string.IsNullOrWhiteSpace(requestDto.线圈货号))
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "线圈货号（关键字）不能为空！"
                });
            }

            var result = await _pmcService.SearchCoilsByKeyword(requestDto.线圈货号);
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "查询成功！",
                Data = result
            });
        }

        /// <summary>
        /// 按货号模糊查询产品资料（不区分线圈，货号包含关键字即可），最多返回 50 条
        /// </summary>
        [HttpPost("SearchProductDataByKeyword")]
        public async Task<ActionResult<ApiResponse<object>>> SearchProductDataByKeyword(PMCRequestDto requestDto)
        {
            if (string.IsNullOrWhiteSpace(requestDto.货号))
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "货号不能为空！"
                });
            }

            var result = await _pmcService.SearchProductDataByKeyword(requestDto.货号);
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
        public async Task<ActionResult<ApiResponse<PagedResult<PMCDeliveryReview>>>> GetPMCDeliveryReviewList(PMCRequestDto requestDto)
        {
            try
            {
                var reviewList = await _pmcService.GetPMCDeliveryReviewList(requestDto, HttpContext.RequestAborted);
                return Ok(new ApiResponse<PagedResult<PMCDeliveryReview>>
                {
                    Success = true,
                    Message = "获取成功！",
                    Data = reviewList // 空列表也正常返回
                });
            }
            catch (Exception)
            {
                throw;
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
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 新增或修改生产类型覆盖（交期评审生产类型手动覆盖，按合同号+排产编号+货号匹配）
        /// </summary>
        [HttpPost("SaveProductionTypeOverride")]
        public async Task<ActionResult<ApiResponse<object>>> SaveProductionTypeOverride(ProductionTypeOverride overrideEntity)
        {
            try
            {
                var result = await _pmcService.SaveProductionTypeOverride(overrideEntity);
                if (result == null)
                {
                    return Ok(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "保存失败，未返回有效数据！"
                    });
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "保存成功！",
                    Data = result
                });
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 将已通过的交期评审退回待评审，并删除本次分析关联数据
        /// </summary>
        [HttpPost("ReturnDeliveryReview")]
        public async Task<ActionResult<ApiResponse<ReturnDeliveryReviewResultDto>>> ReturnDeliveryReview(
            ReturnDeliveryReviewRequestDto request)
        {
            try
            {
                var result = await _pmcService.ReturnDeliveryReview(request);
                return Ok(new ApiResponse<ReturnDeliveryReviewResultDto>
                {
                    Success = true,
                    Message = "已退回待评审",
                    Data = result
                });
            }
            catch (NotFoundException e)
            {
                return NotFound(new ApiResponse<ReturnDeliveryReviewResultDto>
                {
                    Success = false,
                    Message = e.Message
                });
            }
            catch (ConflictException e)
            {
                return Conflict(new ApiResponse<ReturnDeliveryReviewResultDto>
                {
                    Success = false,
                    Message = e.Message
                });
            }
            catch (ValidationException e)
            {
                return BadRequest(new ApiResponse<ReturnDeliveryReviewResultDto>
                {
                    Success = false,
                    Message = e.Message
                });
            }
        }


        /// <summary>
        /// 根据货号获取产品资料
        /// </summary>
        /// <param name="requestDto"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 获取排产分析列表
        /// </summary>
        /// <param name="requestDto"></param>
        /// <returns></returns>
        [HttpPost("SchedulingAnalysisList")]
        public async Task<ActionResult<ApiResponse<object>>> GetSchedulingAnalysisList(PMCRequestDto requestDto)
        {
            try
            {
                var productData = await _pmcService.GetSchedulingAnalysisList(requestDto);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "获取成功！",
                    Data = productData // 空列表也正常返回
                });
            }
            catch (Exception)
            {
                throw;
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
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 获取工单销控表列表
        /// </summary>
        [HttpPost("GetWorkOrderSalesControlList")]
        public async Task<ActionResult<ApiResponse<PagedResult<WorkOrderSalesControl>>>> GetWorkOrderSalesControlList(PMCRequestDto requestDto)
        {
            try
            {
                var result = await _pmcService.GetWorkOrderSalesControlList(requestDto, HttpContext.RequestAborted);
                return Ok(new ApiResponse<PagedResult<WorkOrderSalesControl>>
                {
                    Success = true,
                    Message = "查询成功！",
                    Data = result
                });
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 批量删除工单销控表数据
        /// </summary>
        [HttpPost("DeleteWorkOrderSalesControlList")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteWorkOrderSalesControlList(List<string> ids)
        {
            try
            {
                await _pmcService.DeleteWorkOrderSalesControlList(ids);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "删除成功！"
                });
            }
            catch (Exception)
            {
                throw;
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
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 获取工单销控表明细列表
        /// </summary>
        [HttpPost("GetWorkOrderSalesControlDetailList")]
        public async Task<ActionResult<ApiResponse<PagedResult<WorkOrderSalesControlDetail>>>> GetWorkOrderSalesControlDetailList(PMCRequestDto requestDto)
        {
            try
            {
                var result = await _pmcService.GetWorkOrderSalesControlDetailList(requestDto, HttpContext.RequestAborted);
                return Ok(new ApiResponse<PagedResult<WorkOrderSalesControlDetail>>
                {
                    Success = true,
                    Message = "查询成功！",
                    Data = result
                });
            }
            catch (Exception)
            {
                throw;
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
            catch (Exception)
            {
                throw;
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
            catch (Exception)
            {
                throw;
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
            catch (Exception)
            {
                throw;
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
            catch (Exception)
            {
                throw;
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
            catch (Exception)
            {
                throw;
            }
        }

        #region 外产发运查询与删除

        /// <summary>
        /// 获取外产发运列表
        /// </summary>
        [HttpPost("GetExternalProductionShipmentList")]
        public async Task<ActionResult<ApiResponse<PagedResult<ExternalProductionShipment>>>> GetExternalProductionShipmentList(PMCRequestDto requestDto)
        {
            try
            {
                var result = await _pmcService.GetExternalProductionShipmentList(requestDto, HttpContext.RequestAborted);
                return Ok(new ApiResponse<PagedResult<ExternalProductionShipment>>
                {
                    Success = true,
                    Message = "查询成功！",
                    Data = result
                });
            }
            catch (Exception)
            {
                throw;
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
            catch (Exception)
            {
                throw;
            }
        }

        #endregion

        #region 外产领料查询与删除

        /// <summary>
        /// 获取外产领料列表
        /// </summary>
        [HttpPost("GetExternalProductionPickMaterialList")]
        public async Task<ActionResult<ApiResponse<PagedResult<ExternalProductionPickMaterial>>>> GetExternalProductionPickMaterialList(PMCRequestDto requestDto)
        {
            try
            {
                var result = await _pmcService.GetExternalProductionPickMaterialList(requestDto, HttpContext.RequestAborted);
                return Ok(new ApiResponse<PagedResult<ExternalProductionPickMaterial>>
                {
                    Success = true,
                    Message = "查询成功！",
                    Data = result
                });
            }
            catch (Exception)
            {
                throw;
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
            catch (Exception)
            {
                throw;
            }
        }

        #endregion

        #region 外产生产查询与删除

        /// <summary>
        /// 获取外产生产列表
        /// </summary>
        [HttpPost("GetExternalProductionList")]
        public async Task<ActionResult<ApiResponse<PagedResult<ExternalProduction>>>> GetExternalProductionList(PMCRequestDto requestDto)
        {
            try
            {
                var result = await _pmcService.GetExternalProductionList(requestDto, HttpContext.RequestAborted);
                return Ok(new ApiResponse<PagedResult<ExternalProduction>>
                {
                    Success = true,
                    Message = "查询成功！",
                    Data = result
                });
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 根据编号查询单条外产生产数据
        /// </summary>
        [HttpPost("GetExternalProductionByNo")]
        public async Task<ActionResult<ApiResponse<ExternalProduction>>> GetExternalProductionByNo([FromBody] string 编号)
        {
            try
            {
                var result = await _pmcService.GetExternalProductionByNo(编号);
                if (result == null)
                {
                    return Ok(new ApiResponse<ExternalProduction>
                    {
                        Success = false,
                        Message = "未找到指定编号的数据",
                        Data = null
                    });
                }
                return Ok(new ApiResponse<ExternalProduction>
                {
                    Success = true,
                    Message = "查询成功！",
                    Data = result
                });
            }
            catch (Exception)
            {
                throw;
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
            catch (Exception)
            {
                throw;
            }
        }

        #endregion

        #region 外产入库查询与删除

        /// <summary>
        /// 获取外产入库列表
        /// </summary>
        [HttpPost("GetExternalProductionWarehousingList")]
        public async Task<ActionResult<ApiResponse<PagedResult<ExternalProductionWarehousing>>>> GetExternalProductionWarehousingList(PMCRequestDto requestDto)
        {
            try
            {
                var result = await _pmcService.GetExternalProductionWarehousingList(requestDto, HttpContext.RequestAborted);
                return Ok(new ApiResponse<PagedResult<ExternalProductionWarehousing>>
                {
                    Success = true,
                    Message = "查询成功！",
                    Data = result
                });
            }
            catch (Exception)
            {
                throw;
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
            catch (Exception)
            {
                throw;
            }
        }

        #endregion

        #region 外产BOM查询与删除

        /// <summary>
        /// 根据成品货号生成并保存外产BOM结构
        /// </summary>
        [HttpPost("SaveExternalProductionBOM")]
        public async Task<ActionResult<ApiResponse<object>>> SaveExternalProductionBOM(List<ExternalProductionBOM> bomList, string username, string schedulingNo)
        {
            try
            {
                var savedList = await _pmcService.SaveExternalProductionBOM(bomList, username, schedulingNo);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "保存成功！",
                    Data = savedList
                });
            }
            catch (Exception)
            {
                throw;
            }
        }


       

        /// <summary>
        /// 获取外产BOM列表
        /// </summary>
        [HttpPost("GetExternalProductionBOMList")]
        public async Task<ActionResult<ApiResponse<PagedResult<ExternalProductionBOM>>>> GetExternalProductionBOMList(PMCRequestDto requestDto)
        {
            try
            {
                var result = await _pmcService.GetExternalProductionBOMList(requestDto, HttpContext.RequestAborted);
                return Ok(new ApiResponse<PagedResult<ExternalProductionBOM>>
                {
                    Success = true,
                    Message = "查询成功！",
                    Data = result
                });
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 批量删除外产BOM数据
        /// </summary>
        [HttpPost("DeleteExternalProductionBOMList")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteExternalProductionBOMList(List<string> ids)
        {
            try
            {
                await _pmcService.DeleteExternalProductionBOMList(ids);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "删除成功！"
                });
            }
            catch (Exception)
            {
                throw;
            }
        }

        #endregion

        #region BOM结构工序

        /// <summary>
        /// 获取所有BOM结构工序数据
        /// </summary>
        [HttpPost("GetBOMStructureProcessList")]
        public async Task<ActionResult<ApiResponse<object>>> GetBOMStructureProcessList()
        {
            try
            {
                var result = await _pmcService.GetBOMStructureProcessList();
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "查询成功！",
                    Data = result
                });
            }
            catch (Exception)
            {
                throw;
            }
        }

        #endregion
    }
}
