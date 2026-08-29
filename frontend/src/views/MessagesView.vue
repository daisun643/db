<template>
  <div class="page-container messages-page">
    <div class="messages-nav">
      <div class="tabs">
        <button :class="['tab', { active: activeTab === 'messages' }]" @click="activeTab = 'messages'">
          私信
          <span class="badge badge-red" v-if="unreadMessages > 0">{{ unreadMessages }}</span>
        </button>
        <button :class="['tab', { active: activeTab === 'notifications' }]" @click="activeTab = 'notifications'">
          通知
          <span class="badge badge-red" v-if="notifications.length > 0">{{ notifications.length }}</span>
        </button>
      </div><span>连接每一位同学</span>
    </div>

    <div v-if="error" class="error-message">{{ error }}</div>
    <div v-if="success" class="success-message">{{ success }}</div>

    <div v-if="activeTab === 'messages'" class="messages-layout">
      <aside class="friends-panel">
        <form class="friend-form" @submit.prevent="handleAddFriend">
          <input v-model="friendEmail" type="email" placeholder="好友邮箱" required />
          <button class="btn" type="submit">添加</button>
        </form>

        <section v-if="friendRequests.length > 0" class="request-list">
          <h2>好友申请</h2>
          <div v-for="request in friendRequests" :key="request.friendshipID" class="request-item">
            <span>{{ request.username || request.email }}</span>
            <div class="request-actions">
              <button class="link-button" @click="handleAccept(request)">接受</button>
              <button class="link-button danger" @click="handleReject(request)">拒绝</button>
            </div>
          </div>
        </section>

        <section class="friend-list">
          <div class="list-title-row">
            <h2>好友</h2>
            <small v-if="conversations.length > 0">{{ conversations.length }} 位</small>
          </div>
          <input v-model="friendSearch" class="friend-search" type="search" placeholder="搜索好友 / 邮箱" />

          <div
            v-for="conversation in filteredConversations"
            :key="conversation.friendshipID"
            :class="['friend-row', { active: selectedFriend?.userID === conversation.userID }]"
          >
            <button class="friend-item" @click="selectFriend(conversation)">
              <span class="friend-title-line">
                <span class="friend-name">{{ conversation.username || conversation.email }}</span>
                <span class="badge badge-red conversation-badge" v-if="conversation.unreadCount > 0">
                  {{ conversation.unreadCount }}
                </span>
              </span>
              <span class="conversation-preview">{{ conversationPreview(conversation) }}</span>
              <small class="conversation-time">{{ formatDate(conversation.latestMessageTime) }}</small>
            </button>
            <button class="friend-delete" @click.stop="handleDeleteFriend(conversation)">删除</button>
          </div>

          <div v-if="conversations.length === 0" class="empty-inline">暂无好友，可以通过邮箱添加同学。</div>
          <div v-else-if="filteredConversations.length === 0" class="empty-inline">没有匹配的好友</div>
        </section>
      </aside>

      <main class="conversation-panel">
        <div v-if="!selectedFriend" class="empty-state">
          <p>选择好友开始聊天</p>
        </div>
        <template v-else>
          <div class="conversation-header">
            <h2>{{ selectedFriend.username || selectedFriend.email }}</h2>
          </div>

          <div v-if="loading" class="loading">加载中...</div>
          <div v-else ref="messageListRef" class="message-list">
            <article
              v-for="message in messages"
              :key="message.messageID"
              :class="['message-item', { unread: !message.isRead, mine: message.senderID !== selectedFriend.userID }]"
            >
              <div class="message-content">
                <div class="message-header">
                  <span class="message-sender">{{ message.senderName || '用户' }}</span>
                  <span class="message-time">{{ formatDate(message.sendTime) }}</span>
                </div>
                <p class="message-text">{{ message.content }}</p>
              </div>
            </article>
            <div v-if="messages.length === 0" class="empty-state compact">
              <p>还没有聊天记录，发一句问候吧。</p>
            </div>
          </div>

          <form class="message-form" @submit.prevent="handleSendMessage">
            <input v-model="messageText" type="text" placeholder="输入私信内容" :disabled="sending" required />
            <button class="btn btn-primary" type="submit" :disabled="isSendDisabled">
              {{ sending ? '发送中...' : '发送' }}
            </button>
          </form>
        </template>
      </main>
    </div>

    <div v-if="activeTab === 'notifications'" class="tab-content">
      <div v-if="loading" class="loading">加载中...</div>
      <div v-else-if="notifications.length === 0" class="empty-state">
        <p>暂无通知</p>
      </div>
      <div v-else class="notification-list">
        <article v-for="notification in notifications" :key="notification.notificationID" class="notification-item">
          <div class="notification-icon">!</div>
          <div class="notification-content">
            <h4>{{ notification.title }}</h4>
            <p>{{ notification.content }}</p>
            <span class="notification-time">{{ formatDate(notification.createTime) }}</span>
          </div>
          <button class="link-button danger" @click="handleDeleteNotification(notification)">删除</button>
        </article>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import {
  acceptFriendRequest,
  createFriendRequest,
  deleteFriend,
  deleteNotification,
  getConversations,
  getFriendRequests,
  getMessages,
  getNotifications,
  getSentFriendRequests,
  getUnreadMessageCount,
  markAllMessagesRead,
  rejectFriendRequest,
  sendMessage,
} from '../api'

const activeTab = ref('messages')
const loading = ref(false)
const sending = ref(false)
const unreadMessages = ref(0)
const conversations = ref([])
const friendRequests = ref([])
const sentFriendRequests = ref([])
const messages = ref([])
const notifications = ref([])
const selectedFriend = ref(null)
const messageListRef = ref(null)
const friendEmail = ref('')
const friendSearch = ref('')
const messageText = ref('')
const error = ref(null)
const success = ref(null)
const latestSentFriendRequest = ref(null)
const AUTO_REFRESH_INTERVAL = 5000
let refreshTimer = null
let refreshing = false
const errorMessage = (e) => e.response?.data?.message || e.message || '操作失败'

const filteredConversations = computed(() => {
  const keyword = friendSearch.value.trim().toLowerCase()
  if (!keyword) return conversations.value

  return conversations.value.filter((conversation) => {
    const name = (conversation.username || '').toLowerCase()
    const email = (conversation.email || '').toLowerCase()
    return name.includes(keyword) || email.includes(keyword)
  })
})

const isSendDisabled = computed(() => !selectedFriend.value || sending.value || messageText.value.trim().length === 0)

const latestMessageId = (list) => list.length > 0 ? list[list.length - 1].messageID : null

const scrollMessagesToBottom = async () => {
  await nextTick()
  const list = messageListRef.value
  if (list) list.scrollTop = list.scrollHeight
}

const updateLatestFriendRequestMessage = () => {
  if (!latestSentFriendRequest.value) return

  const latest = sentFriendRequests.value.find(
    (request) => request.friendshipID === latestSentFriendRequest.value.friendshipID,
  )
  if (!latest) return

  latestSentFriendRequest.value = latest
  if (latest.status === 'Pending') {
    success.value = '好友申请已发送，等待对方处理。'
    return
  }

  success.value = null
  latestSentFriendRequest.value = null
}

const syncSelectedConversation = () => {
  if (!selectedFriend.value) return

  const latest = conversations.value.find((conversation) => conversation.friendshipID === selectedFriend.value.friendshipID)
  if (latest) {
    selectedFriend.value = latest
  } else {
    selectedFriend.value = null
    messages.value = []
  }
}

const loadFriends = async () => {
  const [conversationsRes, requestsRes, sentRequestsRes] = await Promise.all([
    getConversations(),
    getFriendRequests(),
    getSentFriendRequests(),
  ])
  conversations.value = conversationsRes.data
  friendRequests.value = requestsRes.data
  sentFriendRequests.value = sentRequestsRes.data
  syncSelectedConversation()
  updateLatestFriendRequestMessage()
}

const loadMessages = async (silent = false, scrollMode = 'always') => {
  if (!selectedFriend.value) {
    messages.value = []
    return
  }

  let shouldScroll = false
  try {
    if (!silent) loading.value = true
    const previousLatestId = latestMessageId(messages.value)
    const res = await getMessages({ userId: selectedFriend.value.userID })
    messages.value = res.data
    const currentLatestId = latestMessageId(messages.value)
    shouldScroll = scrollMode === 'always' || (scrollMode === 'new' && currentLatestId !== previousLatestId)
  } catch (e) {
    if (!silent) error.value = '无法加载私信: ' + (e.response?.data?.message || e.message)
  } finally {
    if (!silent) loading.value = false
  }

  if (shouldScroll) await scrollMessagesToBottom()
}

const loadNotifications = async () => {
  try {
    loading.value = true
    const res = await getNotifications()
    notifications.value = res.data
  } catch (e) {
    error.value = '无法加载通知: ' + (e.response?.data?.message || e.message)
  } finally {
    loading.value = false
  }
}

const loadUnreadCount = async () => {
  const res = await getUnreadMessageCount()
  unreadMessages.value = res.data.count || 0
}

const selectFriend = async (friend) => {
  selectedFriend.value = friend
  await loadMessages()
  await markAllMessagesRead(friend.userID)
  messages.value = messages.value.map((message) =>
    message.senderID === friend.userID ? { ...message, isRead: true } : message,
  )
  await Promise.all([loadUnreadCount(), loadFriends()])
}

const handleAddFriend = async () => {
  try {
    error.value = null
    success.value = null
    const created = await createFriendRequest({ email: friendEmail.value })
    latestSentFriendRequest.value = created.data
    friendEmail.value = ''
    success.value = '好友申请已发送，等待对方处理。'
    await loadFriends()
  } catch (e) {
    success.value = null
    error.value = '好友申请失败: ' + (e.response?.data?.message || e.message)
  }
}

const handleAccept = async (request) => {
  try {
    error.value = null
    await acceptFriendRequest(request.friendshipID)
    await loadFriends()
  } catch (e) {
    error.value = '接受好友申请失败: ' + errorMessage(e)
  }
}

const handleReject = async (request) => {
  try {
    error.value = null
    await rejectFriendRequest(request.friendshipID)
    await loadFriends()
  } catch (e) {
    error.value = '拒绝好友申请失败: ' + errorMessage(e)
  }
}

const handleSendMessage = async () => {
  if (isSendDisabled.value) return

  try {
    sending.value = true
    error.value = null
    await sendMessage({
      receiverID: selectedFriend.value.userID,
      content: messageText.value.trim(),
    })
    messageText.value = ''
    await loadMessages(false, 'always')
    await Promise.all([loadFriends(), loadUnreadCount()])
  } catch (e) {
    error.value = '发送私信失败: ' + errorMessage(e)
  } finally {
    sending.value = false
  }
}

const handleMarkAllRead = async () => {
  if (!selectedFriend.value) return
  try {
    await markAllMessagesRead(selectedFriend.value.userID)
    await Promise.all([loadMessages(), loadUnreadCount(), loadFriends()])
  } catch (e) {
    error.value = '刷新消息失败: ' + errorMessage(e)
  }
}

const handleDeleteFriend = async (friend = selectedFriend.value) => {
  if (!friend || !window.confirm(`确定删除与“${friend.username || friend.email}”的好友关系吗？历史私信会保留。`)) return
  try {
    error.value = null
    await deleteFriend(friend.friendshipID)
    if (selectedFriend.value?.friendshipID === friend.friendshipID) {
      selectedFriend.value = null
      messages.value = []
    }
    await Promise.all([loadFriends(), loadUnreadCount()])
  } catch (e) {
    error.value = '删除好友失败: ' + errorMessage(e)
  }
}

const handleDeleteNotification = async (notification) => {
  await deleteNotification(notification.notificationID)
  await loadNotifications()
}

const formatDate = (value) => {
  if (!value) return ''

  const date = new Date(value)
  const now = new Date()
  const time = date.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' })
  const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  const startOfDate = new Date(date.getFullYear(), date.getMonth(), date.getDate())
  const diffDays = Math.round((startOfToday - startOfDate) / 86400000)

  if (diffDays === 0) return time
  if (diffDays === 1) return `昨天 ${time}`

  const datePart = date.toLocaleDateString('zh-CN', {
    year: date.getFullYear() === now.getFullYear() ? undefined : 'numeric',
    month: '2-digit',
    day: '2-digit',
  })
  return `${datePart} ${time}`
}

const conversationPreview = (conversation) => {
  if (!conversation.latestMessageContent) return '还没有聊天记录'
  return conversation.latestMessageIsMine ? `我：${conversation.latestMessageContent}` : conversation.latestMessageContent
}

const refreshMessagesPanel = async () => {
  if (activeTab.value !== 'messages' || refreshing) return

  refreshing = true
  try {
    await Promise.all([loadFriends(), loadUnreadCount()])
    if (selectedFriend.value) {
      const selectedUserId = selectedFriend.value.userID
      await loadMessages(true, 'new')
      await markAllMessagesRead(selectedUserId)
      messages.value = messages.value.map((message) =>
        message.senderID === selectedUserId ? { ...message, isRead: true } : message,
      )
      await Promise.all([loadUnreadCount(), loadFriends()])
    }
  } catch (e) {
    // 自动刷新失败时不打断用户当前操作，下一轮刷新会继续尝试。
  } finally {
    refreshing = false
  }
}

const startAutoRefresh = () => {
  if (refreshTimer) return
  refreshTimer = window.setInterval(refreshMessagesPanel, AUTO_REFRESH_INTERVAL)
}

const stopAutoRefresh = () => {
  if (!refreshTimer) return
  window.clearInterval(refreshTimer)
  refreshTimer = null
}

watch(activeTab, async (tab) => {
  if (tab === 'messages') {
    await Promise.all([loadFriends(), loadUnreadCount()])
    await loadMessages()
    startAutoRefresh()
  }
  if (tab === 'notifications') {
    stopAutoRefresh()
    await loadNotifications()
  }
})

onMounted(async () => {
  await Promise.all([loadFriends(), loadUnreadCount(), loadNotifications()])
  startAutoRefresh()
})

onUnmounted(() => {
  stopAutoRefresh()
})
</script>

<style scoped>
.success-message {
  background: #dcfce7;
  color: #166534;
  padding: 1rem;
  border-radius: var(--radius);
  border-left: 4px solid #16a34a;
  margin-bottom: 1rem;
}

.messages-layout {
  display: grid;
  grid-template-columns: 340px minmax(0, 1fr);
  gap: 1rem;
  align-items: start;
}

.friends-panel,
.conversation-panel,
.notification-item {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
}

.friends-panel,
.conversation-panel {
  padding: 1rem;
}

.friend-form,
.message-form {
  display: flex;
  gap: 0.5rem;
}

.friend-form input,
.message-form input,
.friend-search {
  flex: 1;
  min-width: 0;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 0.625rem 0.75rem;
  font: inherit;
}

.message-form button:disabled {
  cursor: not-allowed;
  opacity: 0.65;
}

.request-list,
.friend-list {
  margin-top: 1rem;
}

.list-title-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.list-title-row small {
  color: var(--text-secondary);
  font-size: 0.75rem;
}

.request-list h2,
.friend-list h2,
.conversation-header h2 {
  font-size: 1rem;
  margin-bottom: 0.5rem;
}

.friend-search {
  width: 100%;
  box-sizing: border-box;
  margin-bottom: 0.75rem;
}

.request-item,
.friend-row,
.conversation-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.conversation-header {
  max-width: 920px;
  margin: 0 auto;
}

.request-item {
  padding: 0.625rem 0;
}

.request-actions {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-shrink: 0;
}

.friend-row {
  gap: 0.25rem;
  width: 100%;
  border-radius: var(--radius);
  padding: 0.25rem;
}

.friend-row:hover,
.friend-row.active {
  background: var(--bg);
}

.friend-row.active .friend-name {
  color: var(--primary);
}

.friend-item {
  flex: 1;
  min-width: 0;
  border: none;
  background: transparent;
  color: var(--text);
  cursor: pointer;
  padding: 0.625rem 0.75rem;
  text-align: left;
}

.friend-title-line {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}

.friend-name {
  display: block;
  font-size: 0.98rem;
  font-weight: 600;
  margin-bottom: 0.2rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.conversation-badge {
  flex-shrink: 0;
}

.conversation-preview,
.conversation-time {
  color: var(--text-secondary);
  display: block;
  font-size: 0.82rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.conversation-preview {
  margin-bottom: 0.2rem;
}

.friend-delete {
  border: none;
  background: transparent;
  color: #dc2626;
  cursor: pointer;
  flex-shrink: 0;
  font-size: 0.82rem;
  opacity: 0;
  padding: 0.35rem 0.5rem;
  visibility: hidden;
  transition: opacity 0.15s ease;
}

.friend-row:hover .friend-delete,
.friend-row:focus-within .friend-delete {
  opacity: 1;
  visibility: visible;
}

.friend-delete:hover {
  text-decoration: underline;
}

.message-list,
.notification-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-top: 1rem;
}

.message-list {
  min-height: 420px;
  max-width: 920px;
  width: 100%;
  box-sizing: border-box;
  max-height: 58vh;
  overflow-y: auto;
  gap: 0.75rem;
  padding: 1rem;
  background:
    radial-gradient(circle at top left, rgba(37, 99, 235, 0.08), transparent 28%),
    #f8fafc;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  margin: 1rem auto 0;
}

.message-item {
  display: flex;
  align-self: flex-start;
  max-width: min(76%, 520px);
  padding: 0;
  background: transparent;
  border: none;
}

.message-item.mine {
  align-self: flex-end;
  justify-content: flex-end;
}

.message-item.unread {
  filter: drop-shadow(0 8px 18px rgba(37, 99, 235, 0.12));
}

.message-content {
  position: relative;
  min-width: 0;
  width: 100%;
  padding: 0.75rem 0.9rem;
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 1rem 1rem 1rem 0.25rem;
  box-shadow: 0 8px 24px rgba(15, 23, 42, 0.06);
}

.message-item.mine .message-content {
  background: linear-gradient(135deg, #2563eb, #1d4ed8);
  border-color: transparent;
  color: #ffffff;
  border-radius: 1rem 1rem 0.25rem 1rem;
}

.message-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.35rem;
  line-height: 1.2;
}

.message-item.mine .message-header {
  flex-direction: row-reverse;
}

.message-sender {
  font-weight: 600;
  font-size: 0.78rem;
}

.message-time,
.notification-time,
.empty-inline {
  color: var(--text-secondary);
  font-size: 0.75rem;
}

.message-item.mine .message-time {
  color: rgba(255, 255, 255, 0.76);
}

.message-text {
  color: var(--text);
  line-height: 1.6;
  overflow-wrap: anywhere;
  white-space: pre-wrap;
}

.message-item.mine .message-text {
  color: #ffffff;
}

.message-form {
  max-width: 920px;
  margin: 1rem auto 0;
}

.notification-item {
  display: flex;
  gap: 1rem;
  padding: 1rem;
}

.notification-icon {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background: var(--bg);
  color: var(--primary);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  font-weight: 700;
}

.notification-content {
  flex: 1;
}

.notification-content h4 {
  font-size: 0.875rem;
  margin-bottom: 0.25rem;
}

.notification-content p {
  color: var(--text-secondary);
  font-size: 0.875rem;
  margin-bottom: 0.5rem;
}

.link-button {
  border: none;
  background: transparent;
  color: var(--primary);
  cursor: pointer;
  font: inherit;
}

.link-button.danger {
  color: #dc2626;
}

.compact {
  min-height: auto;
  padding: 1.5rem;
}

@media (max-width: 800px) {
  .messages-layout {
    grid-template-columns: 1fr;
  }

  .friend-form,
  .message-form,
  .conversation-header {
    align-items: stretch;
    flex-direction: column;
  }

  .message-item {
    max-width: 88%;
  }
}
</style>

<style scoped>
.messages-page { width:100%; max-width:1480px; margin:0 auto; color:#11172a; }
.messages-nav { display:flex; align-items:center; justify-content:space-between; gap:1rem; margin:1.25rem 0; padding:.55rem; border:1px solid rgba(24,32,55,.08); border-radius:18px; background:#fff; box-shadow:0 10px 30px rgba(28,34,64,.05); }
.messages-nav .tabs { gap:.35rem; margin:0; border:0; }
.messages-nav .tab { margin:0; border:0; border-radius:12px; padding:.72rem 1.05rem; color:#70778a; font-weight:700; }
.messages-nav .tab.active { color:#fff; background:linear-gradient(135deg,#5f50dc,#7967f3); box-shadow:0 8px 20px rgba(95,80,220,.22); }
.messages-nav > span { padding-right:.8rem; color:#9a9fb0; font-size:.72rem; }
.messages-page .messages-layout { width:100%; min-width:0; grid-template-columns:350px minmax(0,1fr); }
.messages-page .friends-panel,.messages-page .conversation-panel { min-width:0; padding:1rem; border:1px solid rgba(25,34,59,.08); border-radius:22px; box-shadow:0 12px 34px rgba(29,35,58,.05); }
.messages-page .friend-form input,.messages-page .message-form input,.messages-page .friend-search { border:1px solid #e2e4ed; border-radius:11px; background:#fafafd; }
.messages-page :is(.friend-form input,.message-form input,.friend-search):focus { border-color:#7463ee; outline:none; box-shadow:0 0 0 4px rgba(105,87,245,.1); }
.messages-page .friend-row { margin:.25rem 0; border-radius:14px; }
.messages-page .friend-row:hover { background:#f7f5ff; }
.messages-page .friend-row.active { background:#efedff; }
.messages-page .message-list { border:0; border-radius:18px; background:radial-gradient(circle at top left,rgba(105,87,245,.1),transparent 28%),#f8f8fc; }
.messages-page .message-item.mine .message-content { background:linear-gradient(135deg,#5f50dc,#7361eb); }
.messages-page .message-content { border-color:#e6e7ed; box-shadow:0 8px 24px rgba(29,35,58,.06); }
.messages-page .message-form .btn-primary { border-radius:11px; background:linear-gradient(135deg,#5f50dc,#7563ef); }
.messages-page .notification-item { border:1px solid rgba(25,34,59,.08); border-radius:18px; box-shadow:0 10px 28px rgba(29,35,58,.04); }
.messages-page .notification-icon { color:#6654e8; background:#efedff; }
@media(max-width:900px){.messages-page .messages-layout{grid-template-columns:minmax(0,1fr)}}
@media(max-width:640px){.messages-nav{overflow-x:auto}.messages-nav > span{display:none}.messages-page .friends-panel,.messages-page .conversation-panel{width:100%;border-radius:18px}.messages-page .friend-form,.messages-page .message-form{width:100%}.messages-page .message-item{max-width:92%}}
</style>
