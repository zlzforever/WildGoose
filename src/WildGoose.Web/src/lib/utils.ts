import CryptoJS from "crypto-js"
// export function uuid() {
//   return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
//     const r = (Math.random() * 16) | 0
//     const v = c === 'x' ? r : (r & 0x3) | 0x8
//     return v.toString(16)
//   })
// }

const uuid = () => {
  return "xxxxxxxxxxxx4xxxyxxxxxxxxxxxxxxx".replace(/[xy]/g, function (c) {
    // 加密安全随机数（关键替换）
    const r = crypto.getRandomValues(new Uint8Array(1))[0] % 16
    const v = c === "x" ? r : (r & 0x3) | 0x8
    return v.toString(16)
  })
}

const randomKey = () => {
  return uuid().slice(0, 6)
}

const AESEnc = (key: string, content: string) => {
  const aesKey = CryptoJS.enc.Utf8.parse(key)
  const srcs = CryptoJS.enc.Utf8.parse(content)
  const iv = CryptoJS.lib.WordArray.random(16)
  const encrypted = CryptoJS.AES.encrypt(srcs, aesKey, {
    mode: CryptoJS.mode.CBC,
    iv: iv,
    padding: CryptoJS.pad.Pkcs7,
  })
  return iv.toString(CryptoJS.enc.Base64) + ":" + encrypted.ciphertext.toString(CryptoJS.enc.Base64)
}

export { uuid, randomKey, AESEnc }
