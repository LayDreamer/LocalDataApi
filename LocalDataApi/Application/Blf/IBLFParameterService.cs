using LocalDataApi.Dto;
using LocalDataApi.Domain.Blf;

namespace LocalDataApi.Application.Blf;

public interface IBLFParameterService
{
    Task<List<BLFParameter>> GetAllParameters();
    Task<BLFParameter?> GetBLFParameter(GetBLFParameterRequest getBLFParameter);
    Task CreateBLFParameter(BLFParameter blfParameter);
    Task UpdateBLFParameter(BLFParameter blfParameter);
    Task DeleteBLFParameter(List<string> numbers);
}
