using LocalDataApi.Infrastructure.WeChatWork;
using SKIT.FlurlHttpClient.Wechat.Work;
using SKIT.FlurlHttpClient.Wechat.Work.Models;

namespace LocalDataApi.Application.WeChatWork;

/// <summary>
/// 企业微信智能表格(文档)用例:文档创建/删除、子表管理、字段与记录读写。
/// </summary>
public class WeChatWorkSmartSheetService : WechatWorkServiceBase
{
    public WeChatWorkSmartSheetService(
        WechatWorkClient client,
        WechatWorkTokenProvider tokenProvider,
        ILogger<WeChatWorkSmartSheetService> logger)
        : base(client, tokenProvider, logger)
    {
    }

    /// <summary>创建一个智能表格</summary>
    public async Task<CgibinWedocCreateDocumentResponse> CreateDocumentAsync(string title, List<string> userIds, string? parentId = null, CancellationToken ct = default)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
        var request = new CgibinWedocCreateDocumentRequest
        {
            AccessToken = accessToken,
            DocumentName = title,
            DocumentType = 10,
            AdminUserIdList = userIds
        };
        return await _client.ExecuteCgibinWedocCreateDocumentAsync(request, ct);
    }

    /// <summary>删除智能表格(doc)</summary>
    public async Task<CgibinWedocDeleteDocumentResponse> DeleteDocumentAsync(string documentId, CancellationToken ct = default)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
        var request = new CgibinWedocDeleteDocumentRequest
        {
            AccessToken = accessToken,
            DocumentId = documentId,
        };
        return await _client.ExecuteCgibinWedocDeleteDocumentAsync(request, ct);
    }

    /// <summary>添加智能表格子表(sheet)</summary>
    public async Task<CgibinWedocSmartSheetAddSheetResponse> SmartSheetAddSheetAsync(string documentId, CancellationToken ct = default)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
        var request = new CgibinWedocSmartSheetAddSheetRequest
        {
            AccessToken = accessToken,
            DocumentId = documentId,
            Sheet = new CgibinWedocSmartSheetAddSheetRequest.Types.Sheet
            {
                Title = "测试表1",
            }
        };
        return await _client.ExecuteCgibinWedocSmartSheetAddSheetAsync(request, ct);
    }

    /// <summary>获取智能表格的所有子表信息</summary>
    public async Task<List<CgibinWedocSmartSheetGetSheetResponse.Types.Sheet>> GetSheetsAsync(string docId, CancellationToken ct = default)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
        var request = new CgibinWedocSmartSheetGetSheetRequest
        {
            AccessToken = accessToken,
            DocumentId = docId
        };

        var response = await _client.ExecuteCgibinWedocSmartSheetGetSheetAsync(request, ct);

        if (response.IsSuccessful() && response.ErrorCode == 0)
        {
            return response.SheetList?.ToList() ?? new List<CgibinWedocSmartSheetGetSheetResponse.Types.Sheet>();
        }
        else
        {
            throw new InvalidOperationException($"查询子表失败: [{response.ErrorCode}] {response.ErrorMessage}");
        }
    }

    /// <summary>向智能表格的指定子表中添加记录(行),缺失字段自动创建</summary>
    public async Task<CgibinWedocSmartSheetAddRecordsResponse> AddSmartSheetRecordsAsync(
        string docId,
        string? sheetId,
        IList<IDictionary<string, object>> records,
        CancellationToken ct = default)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);

        if (string.IsNullOrEmpty(sheetId))
        {
            sheetId = await GetDefaultSheetIdAsync(docId, ct);
        }

        var fields = await GetFieldsAsync(docId, sheetId, ct);
        var fieldNameToIdMap = fields.FieldList.ToDictionary(f => f.Title, f => f.FieldId);
        var fieldIdToTypeMap = fields.FieldList.ToDictionary(f => f.FieldId, f => f.Type);

        var (missingFields, fieldNameToInferredType) = AnalyzeMissingFields(records, fieldNameToIdMap.Keys);

        if (missingFields.Any())
        {
            await CreateMissingFieldsAsync(docId, sheetId, missingFields, fieldNameToInferredType, accessToken);
            fields = await GetFieldsAsync(docId, sheetId, ct);
            fieldNameToIdMap = fields.FieldList.ToDictionary(f => f.Title, f => f.FieldId);
            fieldIdToTypeMap = fields.FieldList.ToDictionary(f => f.FieldId, f => f.Type);
        }

        var recordList = BuildRecordList(records, fieldNameToIdMap, fieldIdToTypeMap);

        var addRequest = new CgibinWedocSmartSheetAddRecordsRequest
        {
            AccessToken = accessToken,
            DocumentId = docId,
            SheetId = sheetId,
            RecordList = recordList,
            KeyType = "CELL_VALUE_KEY_TYPE_FIELD_ID"
        };

        return await _client.ExecuteCgibinWedocSmartSheetAddRecordsAsync(addRequest, ct);
    }

    /// <summary>获取智能表格相关数据信息</summary>
    public async Task<CgibinWedocSmartSheetGetRecordsResponse> GetSmartSheetRecordsAsync(
        string docId,
        string? sheetId,
        CancellationToken ct = default)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
        var request = new CgibinWedocSmartSheetGetRecordsRequest
        {
            AccessToken = accessToken,
            DocumentId = docId,
            SheetId = sheetId,
        };
        return await _client.ExecuteCgibinWedocSmartSheetGetRecordsAsync(request, ct);
    }

    /// <summary>删除智能表格的指定子表中的记录(行)</summary>
    public async Task<CgibinWedocSmartSheetDeleteRecordsResponse> DeleteSmartSheetRecordsAsync(
        string docId,
        string? sheetId,
        IList<string> recordIds,
        CancellationToken ct = default)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
        var request = new CgibinWedocSmartSheetDeleteRecordsRequest
        {
            AccessToken = accessToken,
            DocumentId = docId,
            SheetId = sheetId,
            RecordIdList = recordIds
        };
        return await _client.ExecuteCgibinWedocSmartSheetDeleteRecordsAsync(request, ct);
    }

    /// <summary>更新智能表格的指定子表中的记录(行)</summary>
    public async Task<CgibinWedocSmartSheetUpdateRecordsResponse> UpdateSmartSheetRecordsAsync(
        string docId,
        string? sheetId,
        IList<CgibinWedocSmartSheetUpdateRecordsRequest.Types.Record> recordList,
        CancellationToken ct = default)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
        var request = new CgibinWedocSmartSheetUpdateRecordsRequest
        {
            AccessToken = accessToken,
            DocumentId = docId,
            SheetId = sheetId,
            KeyType = "CELL_VALUE_KEY_TYPE_FIELD_ID",
            RecordList = recordList
        };
        return await _client.ExecuteCgibinWedocSmartSheetUpdateRecordsAsync(request, ct);
    }

    /// <summary>获取默认子表的SheetId</summary>
    public async Task<string> GetDefaultSheetIdAsync(string docId, CancellationToken ct = default)
    {
        var sheets = await GetSheetsAsync(docId, ct);
        var defaultSheet = sheets.FirstOrDefault(s => s.Type == "smartsheet");

        if (defaultSheet != null)
        {
            return defaultSheet.SheetId;
        }
        throw new InvalidOperationException("未找到智能表格类型的子表");
    }

    /// <summary>获取智能表格指定子表的字段列表</summary>
    public async Task<CgibinWedocSmartSheetGetFieldsResponse> GetFieldsAsync(
        string docId,
        string sheetId,
        CancellationToken ct = default)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
        var request = new CgibinWedocSmartSheetGetFieldsRequest
        {
            AccessToken = accessToken,
            DocumentId = docId,
            SheetId = sheetId
        };
        return await _client.ExecuteCgibinWedocSmartSheetGetFieldsAsync(request, ct);
    }

    /// <summary>分析记录中使用的字段,找出缺失字段并推断类型。</summary>
    private (List<string> MissingFields, Dictionary<string, string> FieldNameToInferredType) AnalyzeMissingFields(
        IEnumerable<IDictionary<string, object>> records,
        ICollection<string> existingFieldNames)
    {
        var allFieldNames = new HashSet<string>();
        var fieldNameToInferredType = new Dictionary<string, string>();

        foreach (var record in records)
        {
            foreach (var kv in record)
            {
                var fieldName = kv.Key;
                var value = kv.Value;
                allFieldNames.Add(fieldName);
                if (value != null && !fieldNameToInferredType.ContainsKey(fieldName))
                {
                    fieldNameToInferredType[fieldName] = InferFieldType(value);
                }
            }
        }

        var missingFields = allFieldNames.Where(name => !existingFieldNames.Contains(name)).ToList();

        foreach (var fieldName in missingFields.Where(name => !fieldNameToInferredType.ContainsKey(name)))
        {
            fieldNameToInferredType[fieldName] = "FIELD_TYPE_TEXT";
        }

        return (missingFields, fieldNameToInferredType);
    }

    /// <summary>批量创建缺失的字段。</summary>
    private async Task CreateMissingFieldsAsync(
        string docId,
        string sheetId,
        List<string> missingFieldNames,
        Dictionary<string, string> fieldNameToInferredType,
        string accessToken)
    {
        var fieldsToAdd = missingFieldNames
            .Select(fieldName => CreateFieldWithDefaultProperties(fieldName, fieldNameToInferredType[fieldName]))
            .ToList();

        var addFieldsRequest = new CgibinWedocSmartSheetAddFieldsRequest
        {
            AccessToken = accessToken,
            DocumentId = docId,
            SheetId = sheetId,
            FieldList = fieldsToAdd
        };

        var addFieldsResponse = await _client.ExecuteCgibinWedocSmartSheetAddFieldsAsync(addFieldsRequest);
        if (!addFieldsResponse.IsSuccessful())
        {
            var fieldNames = string.Join(", ", missingFieldNames);
            throw new InvalidOperationException(
                $"创建字段失败 (字段: {fieldNames})。错误码: [{addFieldsResponse.ErrorCode}] {addFieldsResponse.ErrorMessage}");
        }
    }

    /// <summary>根据字段名和推断类型,创建一个带有默认属性设置的 Field 对象。</summary>
    private CgibinWedocSmartSheetAddFieldsRequest.Types.Field CreateFieldWithDefaultProperties(
        string fieldName, string inferredType)
    {
        var field = new CgibinWedocSmartSheetAddFieldsRequest.Types.Field
        {
            Title = fieldName,
            Type = inferredType
        };

        switch (inferredType)
        {
            case "FIELD_TYPE_TEXT":
                field.PropertyAsText = new CgibinWedocSmartSheetAddFieldsRequest.Types.Field.Types.TextFieldProperty();
                break;

            case "FIELD_TYPE_NUMBER":
                field.PropertyAsNumber = new CgibinWedocSmartSheetAddFieldsRequest.Types.Field.Types.NumberFieldProperty
                {
                    DecimalPlaces = 0,
                    IsUseSeparate = false
                };
                break;

            case "FIELD_TYPE_CHECKBOX":
                field.PropertyAsCheckbox = new CgibinWedocSmartSheetAddFieldsRequest.Types.Field.Types.CheckboxFieldProperty
                {
                    IsChecked = false
                };
                break;

            case "FIELD_TYPE_DATE_TIME":
                field.PropertyAsDateTime = new CgibinWedocSmartSheetAddFieldsRequest.Types.Field.Types.DateTimeFieldProperty
                {
                    FormatString = "yyyy-MM-dd",
                    IsAutoFill = false
                };
                break;

            default:
                field.PropertyAsText = new CgibinWedocSmartSheetAddFieldsRequest.Types.Field.Types.TextFieldProperty();
                break;
        }

        return field;
    }

    /// <summary>构建记录列表,替换字段名为字段ID,并根据字段类型构造单元格值对象</summary>
    private List<CgibinWedocSmartSheetAddRecordsRequest.Types.Record> BuildRecordList(
        IEnumerable<IDictionary<string, object>> records,
        IReadOnlyDictionary<string, string> fieldNameToIdMap,
        IReadOnlyDictionary<string, string> fieldIdToTypeMap)
    {
        var recordList = new List<CgibinWedocSmartSheetAddRecordsRequest.Types.Record>();
        foreach (var record in records)
        {
            var values = new Dictionary<string, object>();
            foreach (var kv in record)
            {
                if (!fieldNameToIdMap.TryGetValue(kv.Key, out var fieldId))
                    throw new KeyNotFoundException($"字段 '{kv.Key}' 不存在");
                var fieldType = fieldIdToTypeMap[fieldId];
                values[fieldId] = BuildCellValue(fieldType, kv.Value);
            }
            recordList.Add(new CgibinWedocSmartSheetAddRecordsRequest.Types.Record { Values = values });
        }
        return recordList;
    }

    /// <summary>根据字段类型和原始值构建单元格值对象</summary>
    private object BuildCellValue(string fieldType, object rawValue)
    {
        switch (fieldType)
        {
            case "FIELD_TYPE_TEXT":      // 文本
                return new object[] { new { type = "text", text = rawValue?.ToString() ?? "" } };

            case "FIELD_TYPE_NUMBER":    // 数字
                return Convert.ToDouble(rawValue);

            case "FIELD_TYPE_CHECKBOX":  // 复选框
                return Convert.ToBoolean(rawValue);

            case "FIELD_TYPE_DATE_TIME": // 日期
                if (rawValue is DateTime dateTime)
                {
                    var unixTimeMillis = new DateTimeOffset(dateTime).ToUnixTimeMilliseconds();
                    return unixTimeMillis.ToString();
                }
                return rawValue?.ToString() ?? "";

            case "FIELD_TYPE_IMAGE":     // 图片
                if (rawValue == null)
                {
                    return new object[] { };
                }

                if (rawValue is string imageUrl)
                {
                    return new object[] { new { id = (string)null, title = "", image_url = imageUrl, width = 0, height = 0 } };
                }

                if (rawValue is Dictionary<string, object> imageDict)
                {
                    var id = imageDict.ContainsKey("id") ? imageDict["id"]?.ToString() : null;
                    var title = imageDict.ContainsKey("title") ? imageDict["title"]?.ToString() : "";
                    var imageUrlValue = imageDict.ContainsKey("image_url") ? imageDict["image_url"]?.ToString() : "";
                    var width = imageDict.ContainsKey("width") ? Convert.ToInt32(imageDict["width"]) : 0;
                    var height = imageDict.ContainsKey("height") ? Convert.ToInt32(imageDict["height"]) : 0;

                    return new object[] { new { id = id, title = title, image_url = imageUrlValue, width = width, height = height } };
                }

                return new object[] { new { id = (string)null, title = "", image_url = rawValue.ToString(), width = 0, height = 0 } };

            case "FIELD_TYPE_ATTACHMENT": // 文件
                if (rawValue == null)
                {
                    return new object[] { };
                }

                if (rawValue is string fileUrl)
                {
                    return new object[] { new { name = "", size = 0, file_ext = "", file_id = "", file_url = fileUrl, file_type = "", doc_type = 2 } };
                }

                if (rawValue is Dictionary<string, object> attachmentDict)
                {
                    var name = attachmentDict.ContainsKey("name") ? attachmentDict["name"]?.ToString() : "";
                    var size = attachmentDict.ContainsKey("size") ? Convert.ToInt32(attachmentDict["size"]) : 0;
                    var fileExt = attachmentDict.ContainsKey("file_ext") ? attachmentDict["file_ext"]?.ToString() : "";
                    var fileId = attachmentDict.ContainsKey("file_id") ? attachmentDict["file_id"]?.ToString() : "";
                    var fileUrlValue = attachmentDict.ContainsKey("file_url") ? attachmentDict["file_url"]?.ToString() : "";
                    var fileType = attachmentDict.ContainsKey("file_type") ? attachmentDict["file_type"]?.ToString() : "";
                    var docType = attachmentDict.ContainsKey("doc_type") ? Convert.ToInt32(attachmentDict["doc_type"]) : 2;

                    return new object[] { new { name = name, size = size, file_ext = fileExt, file_id = fileId, file_url = fileUrlValue, file_type = fileType, doc_type = docType } };
                }

                return new object[] { new { name = "", size = 0, file_ext = "", file_id = "", file_url = rawValue.ToString(), file_type = "", doc_type = 2 } };

            case "FIELD_TYPE_USER":      // 成员
                if (rawValue == null)
                {
                    return new object[] { };
                }

                if (rawValue is string userId)
                {
                    return new object[] { new { user_id = userId } };
                }

                if (rawValue is Dictionary<string, object> userDict)
                {
                    var userIdValue = userDict.ContainsKey("user_id") ? userDict["user_id"]?.ToString() : "";
                    return new object[] { new { user_id = userIdValue } };
                }

                return new object[] { new { user_id = rawValue.ToString() } };

            case "FIELD_TYPE_URL":       // 链接
                if (rawValue == null)
                {
                    return new object[] { };
                }

                if (rawValue is string link)
                {
                    return new object[] { new { type = "url", text = link, link = link } };
                }

                if (rawValue is Dictionary<string, object> urlDict)
                {
                    var type = urlDict.ContainsKey("type") ? urlDict["type"]?.ToString() : "url";
                    var text = urlDict.ContainsKey("text") ? urlDict["text"]?.ToString() : "";
                    var linkValue = urlDict.ContainsKey("link") ? urlDict["link"]?.ToString() : "";

                    return new object[] { new { type = type, text = text, link = linkValue } };
                }

                return new object[] { new { type = "url", text = rawValue.ToString(), link = rawValue.ToString() } };

            case "FIELD_TYPE_SELECT":    // 多选
            case "FIELD_TYPE_SINGLE_SELECT": // 单选
                if (rawValue == null)
                {
                    return new object[] { };
                }

                if (rawValue is string selectText)
                {
                    return new object[] { new { id = (string)null, style = 0, text = selectText } };
                }

                if (rawValue is Dictionary<string, object> selectDict)
                {
                    var id = selectDict.ContainsKey("id") ? selectDict["id"]?.ToString() : null;
                    var style = selectDict.ContainsKey("style") ? Convert.ToInt32(selectDict["style"]) : 0;
                    var text = selectDict.ContainsKey("text") ? selectDict["text"]?.ToString() : "";
                    return new object[] { new { id = id, style = style, text = text } };
                }

                return new object[] { new { id = (string)null, style = 0, text = rawValue.ToString() } };

            case "FIELD_TYPE_PROGRESS":  // 进度
                return Convert.ToDouble(rawValue);

            case "FIELD_TYPE_PHONE_NUMBER": // 电话
                return rawValue?.ToString() ?? "";

            case "FIELD_TYPE_EMAIL":    // 邮箱
                return rawValue?.ToString() ?? "";

            case "FIELD_TYPE_LOCATION":  // 地理位置
                if (rawValue == null)
                {
                    return new object[] { };
                }

                if (rawValue is string locationTitle)
                {
                    return new object[] {
                        new {
                            source_type = 1,
                            id = "",
                            latitude = "",
                            longitude = "",
                            title = locationTitle
                        }
                    };
                }

                if (rawValue is Dictionary<string, object> locationDict)
                {
                    var sourceType = locationDict.ContainsKey("source_type") ? Convert.ToUInt32(locationDict["source_type"]) : 1;
                    var id = locationDict.ContainsKey("id") ? locationDict["id"]?.ToString() : "";
                    var latitude = locationDict.ContainsKey("latitude") ? locationDict["latitude"]?.ToString() : "";
                    var longitude = locationDict.ContainsKey("longitude") ? locationDict["longitude"]?.ToString() : "";
                    var title = locationDict.ContainsKey("title") ? locationDict["title"]?.ToString() : "";

                    return new object[] {
                        new {
                            source_type = sourceType,
                            id = id,
                            latitude = latitude,
                            longitude = longitude,
                            title = title
                        }
                    };
                }

                if (rawValue is ValueTuple<string, string, string> locationTuple)
                {
                    return new object[] {
                        new {
                            source_type = 1,
                            id = "",
                            latitude = locationTuple.Item1,
                            longitude = locationTuple.Item2,
                            title = locationTuple.Item3
                        }
                    };
                }

                return new object[] {
                    new {
                        source_type = 1,
                        id = "",
                        latitude = "",
                        longitude = "",
                        title = rawValue.ToString()
                    }
                };

            case "FIELD_TYPE_CURRENCY": // 货币
                return Convert.ToDouble(rawValue);

            case "FIELD_TYPE_PERCENTAGE": // 百分数
                return Convert.ToDouble(rawValue);

            case "FIELD_TYPE_BARCODE":  // 条码
                return rawValue?.ToString() ?? "";

            default:
                throw new NotSupportedException($"不支持的字段类型: {fieldType}");
        }
    }

    /// <summary>根据值的类型推断字段类型(支持常见类型)</summary>
    private string InferFieldType(object value)
    {
        if (value == null)
            return "text"; // 空值无法推断,默认文本

        switch (value)
        {
            case int _:
            case long _:
            case float _:
            case double _:
            case decimal _:
                return "FIELD_TYPE_NUMBER";

            case bool _:
                return "FIELD_TYPE_CHECKBOX";

            case DateTime _:
                return "FIELD_TYPE_DATE_TIME";

            case string str:
                if (DateTime.TryParse(str, out _))
                    return "FIELD_TYPE_DATE_TIME";
                if (bool.TryParse(str, out _))
                    return "FIELD_TYPE_CHECKBOX";
                if (decimal.TryParse(str, out _))
                    return "FIELD_TYPE_NUMBER";
                return "FIELD_TYPE_TEXT";

            default:
                return "FIELD_TYPE_TEXT";
        }
    }
}
