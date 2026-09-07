// SSE 实时推送单例：全站共用一条 /api/notifications/stream 连接。
// 事件类型（与后端 NotificationsController.Stream 对应）：
//   notification —— 新通知信号（载荷不含稳定 ID），订阅方应重新拉取列表与未读数
//   message      —— 新私信完整消息体（messageID/content/senderID/...），可直接渲染
// 连接生命周期由 App.vue 根据登录状态管理；断线由 EventSource 自动重连，
// 重连成功触发 open 回调做一次全量刷新兜底。

const EVENT_TYPES = ['notification', 'message']
const RECONNECT_DELAY = 10000

let source = null
const openSubscribers = new Set()
const eventSubscribers = {
  notification: new Set(),
  message: new Set(),
}

const safeCall = (cb, ...args) => {
  try {
    cb(...args)
  } catch {
    // 单个订阅方异常不影响其他订阅方
  }
}

const dispatchOpen = () => openSubscribers.forEach((cb) => safeCall(cb))

const dispatchEvent = (type, payload) => {
  eventSubscribers[type]?.forEach((cb) => safeCall(cb, payload))
}

export function startStream() {
  if (source) return
  source = new EventSource('/api/notifications/stream')

  source.onopen = dispatchOpen
  source.onerror = () => {
    // 网络闪断时 readyState 为 CONNECTING，浏览器会自动重连；
    // 仅当彻底关闭（如初次连接失败）时清理引用并安排重试
    if (source && source.readyState === EventSource.CLOSED) {
      source = null
      setTimeout(() => {
        if (!source) startStream()
      }, RECONNECT_DELAY)
    }
  }

  for (const type of EVENT_TYPES) {
    source.addEventListener(type, (e) => {
      let payload = null
      try {
        payload = JSON.parse(e.data)
      } catch {
        payload = null
      }
      dispatchEvent(type, payload)
    })
  }
}

export function stopStream() {
  if (!source) return
  source.close()
  source = null
}

// 注册“连接建立/重连成功”回调，返回解绑函数
export function onStreamOpen(cb) {
  openSubscribers.add(cb)
  return () => openSubscribers.delete(cb)
}

// 注册事件回调（type: 'notification' | 'message'），返回解绑函数
export function onStreamEvent(type, cb) {
  eventSubscribers[type]?.add(cb)
  return () => eventSubscribers[type]?.delete(cb)
}
