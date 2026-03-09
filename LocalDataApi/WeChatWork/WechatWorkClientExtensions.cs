using SKIT.FlurlHttpClient.Wechat.Work;

namespace LocalDataApi.WeChatWork
{
    public static class WechatWorkClientExtensions
    {
        public static IHttpClientBuilder AddWechatWorkClient(
            this IServiceCollection services,
            Action<IServiceProvider, WechatWorkClientOptions> configure)
        {
            return services.AddHttpClient<WechatWorkClient>((serviceProvider, httpClient) =>
            {
                var options = new WechatWorkClientOptions();
                configure(serviceProvider, options);
                new WechatWorkClient(options);
            });
        }
    }
}
