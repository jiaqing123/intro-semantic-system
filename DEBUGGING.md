# 启动与调试经验总结

> 适用于 `SemanticSystemConsole`（.NET 10 控制台程序 + Semantic Kernel + DeepSeek API，Docker 容器化）。

## 一、程序行为要点

- **一次性控制台程序**：启动 → 调用 DeepSeek API（`kernel.InvokePromptAsync`）→ `Console.WriteLine` 打印回复 → **进程退出，容器随之停止**。
- **密钥来源**：`SemanticSystemConsole/AiApiOptions.cs` 读取环境变量 `DS_API_INTRO_AI`（`.env` 文件提供）；BaseUrl 默认 `https://api.deepseek.com`，模型 `deepseek-v4-flash`。
- **容器里的控制台程序没有可见窗口**：所有输出进入容器的 stdout，只能从日志查看。

## 二、运行方式（按推荐程度）

### 1. 本地直接运行（最快，适合日常验证）

```powershell
# 注入 .env 里的密钥（dotnet run 不会自动读 .env）
$env:DS_API_INTRO_AI = (Get-Content .env | Where-Object { $_ -match '^DS_API_INTRO_AI=' } | ForEach-Object { $_.Substring($_.IndexOf('=')+1) })
dotnet run --project SemanticSystemConsole
```

输出直接打在终端。

### 2. Docker 完整镜像（推荐用于验证镜像本身）

VS 调试镜像不含程序，必须用 Dockerfile 构建完整镜像（默认走 `final` 阶段，入口为 `dotnet SemanticSystemConsole.dll`）：

```powershell
docker build -f SemanticSystemConsole/Dockerfile -t semanticsystemconsole:run .
docker run --rm --env-file .env semanticsystemconsole:run
```

> 必须带 `--env-file .env`，否则容器内拿不到 API 密钥。

### 3. Docker Compose（本地开发）

```powershell
docker compose up --build -d
docker compose logs -f semanticsystemconsole
```

> **必须带 `--build`**：不带会复用旧的调试镜像，容器可能"秒退"且无任何输出。
> 密钥从项目根目录 `.env` 自动加载（模板：`.env.example`），无需手动注入。

### 4. Docker Compose（生产部署）

```powershell
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build
docker compose -f docker-compose.yml -f docker-compose.prod.yml logs -f semanticsystemconsole
```

生产覆盖关闭 tty、使用 Release 构建，镜像标签可用 `APP_VERSION` 指定。

### 5. Visual Studio F5 调试

- 输出显示在 **视图 → 输出窗口 → 下拉选择"容器"（Container）**（以及"调试"）。
- 停止调试后，输出窗口内容**不会保留**。

## 三、查看输出的方法

| 场景 | 方法 |
|---|---|
| 容器正在运行 | `docker logs -f <容器名>`；或 Docker Desktop → Containers → 对应容器 → Logs 标签 |
| 容器已退出 | `docker ps -a` 找到容器名 → `docker logs <容器名>`（一次性程序退出后内容仍在） |
| VS 调试中 | 输出窗口 → "容器" 类别 |

## 四、踩坑记录（都是实际遇到过的）

1. **VS 调试容器 ≠ 你的程序容器**：VS（Debug 配置 + fast mode）启动的容器里，真正进程是 `dotnet /VSTools/DistrolessHelper/DistrolessHelper.dll --wait`（等 VS 指令），程序本体由 VS 通过**卷挂载**（`SemanticSystemConsole/bin/Debug/net10.0`）映射启动。此时 `docker logs` 为空是正常的。
2. **`semanticsystemconsole:dev` 镜像不能单独运行**：它只包含 `base` 阶段（仅有 .NET 运行时），`ENTRYPOINT=null`、`CMD=["/bin/bash"]`——`docker run` 它等于跑一个没终端的 bash，**立即退出（exit 0）且零输出**，极易误判为"程序没问题但没输出"。
3. **`docker compose up` 不带 `--build` 会复用旧调试镜像**，同样出现"秒退无输出"。
4. **exit 0 + 无输出**时按顺序排查：
   - `docker inspect <镜像> --format '{{json .Config.Entrypoint}} {{json .Config.Cmd}}'` 确认入口是 `dotnet ...dll` 而不是 bash；
   - `docker top <容器>` 确认容器里真的有 `SemanticSystemConsole` 进程；
   - 确认 API 密钥已传入（`docker inspect <容器>` 看 `Config.Env`，注意别把密钥内容发出去）；
   - 本地 `dotnet run` 能出结果 → 说明程序没问题，问题在镜像/入口/环境变量。
5. **`.env` 不会自动注入** `docker run` / `dotnet run`，只有 docker compose 会自动加载项目目录的 `.env`。`.env` 已被 .gitignore 忽略、不会提交，模板见 `.env.example`（复制为 .env 后填入真实密钥；compose 中必填变量缺失时会快速失败报错，而不是带空密钥静默运行）。
6. **调试结束后 VS 可能残留容器**（helper 等待状态），占用资源且容易被误认为是"正在运行的程序"：`docker rm -f <容器名>` 手动清理；测试产生的 compose 容器用 `docker compose down` 清理。

## 五、常用命令速查

```powershell
docker ps -a                          # 所有容器（含已退出的）
docker logs <容器名>                   # 查看输出
docker logs -f <容器名>                # 跟随输出
docker top <容器名>                    # 容器内进程（判断程序是否真的在跑）
docker inspect <容器名/镜像>            # 详情：状态、环境变量、入口
docker build -f SemanticSystemConsole/Dockerfile -t semanticsystemconsole:run .
docker run --rm --env-file .env semanticsystemconsole:run
docker compose up --build -d
docker compose logs -f semanticsystemconsole
docker compose down                   # 清理 compose 容器和网络
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build   # 生产部署
copy .env.example .env                # 首次配置：从模板创建本地 .env
docker rm -f <容器名>                  # 强制删除残留容器
```
