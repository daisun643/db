import { onUnmounted, ref } from 'vue'

export const useTimedMessage = () => {
  const message = ref(null)
  let timer = null

  const clearMessage = () => {
    if (timer) clearTimeout(timer)
    timer = null
    message.value = null
  }

  const showMessage = (type, text) => {
    clearMessage()
    message.value = { type, text }
    timer = setTimeout(clearMessage, 3000)
  }

  onUnmounted(clearMessage)

  return { message, clearMessage, showMessage }
}
