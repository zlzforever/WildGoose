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
    const r = (Math.random() * 16) | 0
    const v = c === "x" ? r : (r & 0x3) | 0x8
    return v.toString(16)
  })
}

const randomKey = () => {
  return Array.from({ length: 6 }, () => {
    const charPool =
      "0123456zKLM7deklmnfghxKLMNOy34567zABCDEFijklmnopqrstuvwGHIJ67zABOyzKLMNOPQRS89abcTUVWXYZ"
    return charPool.charAt(Math.floor(Math.random() * charPool.length))
  }).join("")
}

const AESEnc = (key: string, content: string) => {
  const aesKey = CryptoJS.enc.Utf8.parse(key)
  const srcs = CryptoJS.enc.Utf8.parse(content)
  const encrypted = CryptoJS.AES.encrypt(srcs, aesKey, {
    mode: CryptoJS.mode.ECB,
    padding: CryptoJS.pad.Pkcs7,
  })
  return encrypted.ciphertext.toString(CryptoJS.enc.Base64)
}

export { uuid, randomKey, AESEnc }