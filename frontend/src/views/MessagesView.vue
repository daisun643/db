<template>
  <div class="page-container">
    <div class="page-header page-header-tabs">
      <h1 class="page-title">消息中心</h1>

      <div class="tabs">
        <button :class="['tab', { active: activeTab === 'messages' }]" @click="activeTab = 'messages'">
          私信
          <span class="badge badge-red" v-if="unreadMessages > 0">{{ unreadMessages }}</span>
        </button>
        <button :class="['tab', { active: activeTab === 'notifications' }]" @click="activeTab = 'notifications'">
          通知
          <span class="badge badge-red" v-if="notifications.length > 0">{{ notifications.length }}</span>
        </button>
      </div>
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
          <h2>好友</h2>
          <div
            v-for="friend in friends"
            :key="friend.friendshipID"
            :class="['friend-row', { active: selectedFriend?.userID === friend.userID }]"
          >
            <button class="friend-item" @click="selectFriend(friend)">
              <span class="friend-name">{{ friend.username || friend.email }}</span>
              <small>{{ friend.email }}</small>
            </button>
            <button class="friend-delete" @click.stop="handleDeleteFriend(friend)">删除</button>
          </div>
          <div v-if="friends.length === 0" class="empty-inline">暂无好友</div>
        </section>
      </aside>

      <main class="conversation-panel">
        <div v-if="!selectedFriend" class="empty-state">
          <p>选择一个好友开始私信</p>
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
              <p>暂无私信</p>
            </div>
          </div>

          <form class="message-form" @submit.prevent="handleSendMessage">
            <input v-model="messageText" type="text" placeholder="输入私信内容" required />
            <button class="btn btn-primary" type="submit">发送</button>
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
import { nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import {
  acceptFriendRequest,
  createFriendRequest,
  deleteFriend,
  deleteNotification,
  getFriendRequests,
  getFriends,
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
const unreadMessages = ref(0)
const friends = ref([])
const friendRequests = ref([])
const sentFriendRequests = ref([])
const messages = ref([])
const notifications = ref([])
const selectedFriend = ref(null)
const messageListRef = ref(null)
const friendEmail = ref('')
const messageText = ref('')
const error = ref(null)
const success = ref(null)
const latestSentFriendRequest = ref(null)
const AUTO_REFRESH_INTERVAL = 5000
let refreshTimer = null
let refreshing = false
const errorMessage = (e) => e.response?.data?.message || e.message || '操作失败'



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
    success.value = `好友申请已发送，等待对方处理。`
    return
  }

  success.value = null
  latestSentFriendRequest.value = null
}
const loadFriends = async () => {
  const [friendsRes, requestsRes, sentRequestsRes] = await Promise.all([
    getFriends(),
    getFriendRequests(),
    getSentFriendRequests(),
  ])
  friends.value = friendsRes.data
  friendRequests.value = requestsRes.data
  sentFriendRequests.value = sentRequestsRes.data
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
  await loadUnreadCount()
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
  if (!selectedFriend.value) return
  try {
    error.value = null
    await sendMessage({
      receiverID: selectedFriend.value.userID,
      content: messageText.value,
    })
    messageText.value = ''
    await loadMessages()
  } catch (e) {
    error.value = '发送私信失败: ' + errorMessage(e)
  }
}


const handleMarkAllRead = async () => {
  if (!selectedFriend.value) return
  try {
    await markAllMessagesRead(selectedFriend.value.userID)
    await Promise.all([loadMessages(), loadUnreadCount()])
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
  return new Date(value).toLocaleString('zh-CN', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
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
      await loadUnreadCount()
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
.message-form input {
  flex: 1;
  min-width: 0;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 0.625rem 0.75rem;
  font: inherit;
}

.request-list,
.friend-list {
  margin-top: 1rem;
}

.request-list h2,
.friend-list h2,
.conversation-header h2 {
  font-size: 1rem;
  margin-bottom: 0.5rem;
}

.request-item,
.friend-item,
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
  display: flex;
  align-items: center;
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

.friend-name {
  display: block;
  font-size: 0.98rem;
  font-weight: 600;
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

.friend-item small {
  color: var(--text-secondary);
  display: block;
  font-size: 0.82rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
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
