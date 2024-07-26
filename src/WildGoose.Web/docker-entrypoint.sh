echo $BASE_PATH
sed -i "s#%BaseName%#${BASE_PATH}#g" /app/config.js >/app/config2.js
sed -i "s#/config.js#${BASE_PATH}config2.js#g" /app/index.html
sed -i "s#/assets/index-#${BASE_PATH}assets/index-#g" /app/index.html
