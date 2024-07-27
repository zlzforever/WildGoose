#!/bin/bash
set -eu

# 检查输入文件是否存在
if [ -f "${CONFIG_SOURCE}" ]; then
   # 读取文件内容，并替换环境变量
   while IFS= read -r line; do
       eval "echo \"$line\""
   done <"${CONFIG_SOURCE}" >"/app/appsettings.json"
   echo "应用配置文件已生成"
fi

exec "$@"


