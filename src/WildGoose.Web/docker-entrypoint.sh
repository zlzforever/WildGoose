#!/bin/sh
set -eu

version=$(date +%s)
sed -i "s#/config.js#${BASE_PATH}config_${version}.js#g" /app/index.html
sed -i "s#/assets/index-#${BASE_PATH}assets/index-#g" /app/index.html

# 输入文件名
input_file="/app/config.js"
# 输出文件名
output_file="/app/config_${version}.js"

if [ -f "${input_file}" ]; then
    awk '{
       while (match($0, /\$\{[A-Za-z_][A-Za-z0-9_]*\}/)) {
           var=substr($0, RSTART+2, RLENGTH-3)
           gsub(/\$\{[A-Za-z_][A-Za-z0-9_]*\}/, ENVIRON[var])
       }
       print
   }' "$input_file" >"$output_file"
fi
