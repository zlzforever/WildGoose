#!/bin/bash
set -eu

# 定义要处理的输入文件
if [ -f "${CONFIG_SOURCE}" ]; then
    target="/app/appsettings.json"

    # 读取输入文件内容
    content=$(cat "${CONFIG_SOURCE}")

    # 进行环境变量替换
    replaced_content=${content//\${ENV_VAR_NAME}/${!ENV_VAR_NAME}}

    # 将替换后的内容写入输出文件
    echo "$replaced_content" > "${target}"
else
    echo "未设置配置源文件"
fi
