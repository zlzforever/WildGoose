#!/bin/bash

# 输入文件名
input_file="${CONFIG_SOURCE}"
# 输出文件名
output_file="/app/appsettings.json"

# 检查输入文件是否存在
if [ -f "${input_file}" ]; then
   awk '{
       while (match($0, /\$\{[A-Za-z_][A-Za-z0-9_]*\}/)) {
           var = substr($0, RSTART + 2, RLENGTH - 3)
           # 只替换【当前匹配到的这一个】变量，而不是全部替换
           before = substr($0, 1, RSTART - 1)
           after = substr($0, RSTART + RLENGTH)
           $0 = before ENVIRON[var] after
       }
       print
   }' "$input_file" > "$output_file"
   echo "配置文件已生成"
else
   echo "使用默认配置文件"
fi

# 检查环境变量 DAPR_URL 是否存在且不为空
if [ -n "$DAPR_URL" ]; then
    echo "检测到 DAPR_URL， 正在下载内容..."
    # 使用 curl 下载 URL 内容
    curl -O "$DAPR_URL"
    # 检查 curl 命令是否执行成功
    if [ $? -eq 0 ]; then
        echo -e "\n下载 daprd 完成"
        chmod +x daprd
    else
        echo -e "\n下载 daprd 失败"
    fi
fi

exec "$@"


