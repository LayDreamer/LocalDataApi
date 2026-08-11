# 18 服务器前端统一 DNS 与企业微信访问解决方案

## 1. 目标

将前端统一部署在 `192.168.1.18`，让所有内网电脑通过企业 DNS 自动访问，无需逐台维护 hosts 文件。

本方案继续使用现有域名：

```text
www.ycdcf.com
```

这是当前最小风险方案：企业微信可信域名、应用主页和后端回调白名单已经使用该域名，无需因更换域名再次配置或验证。

## 2. 最终架构

```text
企业微信 / 浏览器客户端
  └─ 内网 DNS：www.ycdcf.com → 192.168.1.18
       └─ IIS 前端：80（主机名 www.ycdcf.com）
            └─ LocalDataApi：192.168.1.18:90
```

保留现有 `:1001` 前端绑定，用于技术测试或直接 IP 访问；企业微信应用主页使用标准 HTTP 80 端口，不使用 `:1001`。

## 3. 前端与后端服务器配置

### 3.1 18 上的 IIS 前端绑定

前端站点 `yclt36-curve-viewer` 的物理路径为：

```text
F:\YCDataSystem\dist
```

应同时保留以下绑定：

```text
*:1001:
*:80:www.ycdcf.com
```

其中 `*:80:www.ycdcf.com` 是企业微信应用主页不带端口时必需的入口。

### 3.2 防火墙

18 的 Windows 防火墙应允许内网访问：

```text
TCP 80，RemoteAddress = LocalSubnet
TCP 1001，RemoteAddress = LocalSubnet
TCP 90，仅允许前端客户端所在内网访问
```

不要将后端 `:90` 直接暴露到互联网。

### 3.3 后端 CORS 与企业微信回调

`localDataApi` 必须至少允许以下来源：

```text
http://www.ycdcf.com
http://www.ycdcf.com:1001
http://192.168.1.18:1001
```

企业微信回调白名单需包含：

```text
www.ycdcf.com
```

企业微信应用主页保持：

```text
http://www.ycdcf.com/#/wechat-login
```

企业微信“网页授权及 JS-SDK”可信域名填写：

```text
www.ycdcf.com
```

不要填写协议、端口或路径。

## 4. 网络管理员：配置内网 DNS

在公司 DNS 服务器（通常是 Windows AD DNS）中创建或修改 A 记录：

```text
记录名：www.ycdcf.com
记录类型：A
记录值：192.168.1.18
TTL：切换期间建议 5 分钟；稳定后可调整为 1 小时
```

如果 DNS 使用正向查找区域，则在 `ycdcf.com` 区域下创建主机记录 `www`，其 IP 为 `192.168.1.18`。

> 不要将公网 DNS A 记录直接指向 `192.168.1.18`，因为它是私有 IP。本文方案仅适用于公司内网。若员工需要在外网或手机蜂窝网络访问，请另行建设公网 HTTPS 443 反向代理或 VPN。

## 5. 清理现有客户端 hosts

hosts 文件优先级高于 DNS。只要客户端仍有下面任意记录，统一 DNS 都不会生效：

```text
192.168.1.110 www.ycdcf.com
192.168.1.18  www.ycdcf.com
```

因此应按以下顺序执行：

1. 先在内网 DNS 中将 `www.ycdcf.com` 解析到 `192.168.1.18`。
2. 在一台未写 hosts 的测试客户端验证 DNS 和页面。
3. 验证成功后，通过域控 GPO、Intune、软件分发工具或管理员脚本，统一删除客户端 hosts 中的 `www.ycdcf.com` 静态条目。
4. 客户端执行 `ipconfig /flushdns`，然后重新打开企业微信。

不要先批量删除 hosts，再去配置 DNS；否则用户会暂时无法访问系统。

## 6. 切换验证

### 6.1 DNS 验证

在没有 `www.ycdcf.com` hosts 条目的客户端执行：

```powershell
Resolve-DnsName www.ycdcf.com
```

预期 A 记录为：

```text
192.168.1.18
```

### 6.2 前端验证

```powershell
Test-NetConnection www.ycdcf.com -Port 80
Invoke-WebRequest 'http://www.ycdcf.com/' -UseBasicParsing
```

预期 TCP 连通，首页返回 HTTP 200。

### 6.3 登录与企业微信验证

- 浏览器访问 `http://www.ycdcf.com/`，确认可打开前端登录页。
- 登录后确认 Network 中 API 请求指向 `http://192.168.1.18:90`，无 CORS 错误。
- 从企业微信工作台打开应用，确认进入 `#/wechat-login` 并完成自动登录。

## 7. 回滚

若切换失败，网络管理员将内网 DNS 的 A 记录恢复为：

```text
www.ycdcf.com → 192.168.1.110
```

然后在客户端执行：

```powershell
ipconfig /flushdns
```

企业微信应用主页无需修改，仍使用同一个域名；DNS 恢复后会重新访问旧服务器。

## 8. 后续 HTTPS 与公网访问建议

当前内网 HTTP 80 方案可满足企业微信内网使用。若后续需要手机外网访问，应升级为：

```text
https://www.ycdcf.com
公网 DNS → 公网网关 / 反向代理 → 18 前端
```

要求：

- 使用有效 TLS 证书。
- 对外仅开放 HTTPS 443。
- 后端 `:90` 留在内网，由反向代理转发 `/api` 请求。
- 前端重新构建为同源 `/api` 访问，减少 CORS 与端口暴露风险。
