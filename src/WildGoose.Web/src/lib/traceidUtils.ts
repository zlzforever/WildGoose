const generateTraceId = () => {
  const traceId = generateString(16)
  const spanId = generateString(8)
  return `00-${traceId}-${spanId}-01`
}

const generateString = (length: number) => {
  const buffer = new Uint8Array(length)
  crypto.getRandomValues(buffer)

  while (buffer.every((byte) => byte === 0)) {
    crypto.getRandomValues(buffer)
  }

  return Array.from(buffer)
    .map((byte) => byte.toString(16).padStart(2, "0"))
    .join("")
    .toLowerCase()
}

export { generateString, generateTraceId }
