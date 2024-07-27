#!/bin/bash
set -eu

# 检查输入文件是否存在
if [ ! -f "${CONFIG_SOURCE}" ]; then
    echo "未设置配置源文件或源源文件不存在"
    exit 1
fi

# 读取文件内容，并替换环境变量
while IFS= read -r line; do
    eval "echo \"$line\""
done <"${CONFIG_SOURCE}" >"/app/appsettings.json"
