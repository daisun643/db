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
            <div>
              <button class="link-button" @click="handleAccept(request)">接受</button>
              <button class="link-button danger" @click="handleReject(request)">拒绝</button>
            </div>
          </div>
        </section>

        <section class="friend-list">
          <h2>好友</h2>
          <button
            v-for="friend in friends"
            :key="friend.friendshipID"
            :class="['friend-item', { active: selectedFriend?.userID === friend.userID }]"
            @click="selectFriend(friend)"
          >
            <span>{{ friend.username || friend.email }}</span>
            <small>{{ friend.email }}</small>
          </button>
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
            <div class="conversation-actions">
              <button class="btn" @click="handleMarkAllRead">当前会话已读</button>
              <button class="link-button danger" @click="handleDeleteFriend">删除好友</button>
            </div>
          </div>

          <div v-if="loading" class="loading">加载中...</div>
          <div v-else class="message-list">
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
              <button v-if="!message.isRead && message.receiverID !== selectedFriend.userID" class="link-button" @click="handleRead(message)">
                标为已读
              </button>
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
import { onMounted, ref, watch } from 'vue'
import {
  acceptFriendRequest,
  createFriendRequest,
  deleteFriend,
  deleteNotification,
  getFriendRequests,
  getFriends,
  getMessages,
  getNotifications,
  getUnreadMessageCount,
  markAllMessagesRead,
  markMessageRead,
  rejectFriendRequest,
  sendMessage,
} from '../api'

const activeTab = ref('messages')
const loading = ref(false)
const unreadMessages = ref(0)
const friends = ref([])
const friendRequests = ref([])
const messages = ref([])
const notifications = ref([])
const selectedFriend = ref(null)
const friendEmail = ref('')
const messageText = ref('')
const error = ref(null)
const errorMessage = (e) => e.response?.data?.message || e.message || '操作失败'


const loadFriends = async () => {
  const [friendsRes, requestsRes] = await Promise.all([getFriends(), getFriendRequests()])
  friends.value = friendsRes.data
  friendRequests.value = requestsRes.data
}

const loadMessages = async () => {
  if (!selectedFriend.value) {
    messages.value = []
    return
  }

  try {
    loading.value = true
    const res = await getMessages({ userId: selectedFriend.value.userID })
    messages.value = res.data
  } catch (e) {
    error.value = '无法加载私信: ' + (e.response?.data?.message || e.message)
  } finally {
    loading.value = false
  }
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
    await createFriendRequest({ email: friendEmail.value })
    friendEmail.value = ''
    await loadFriends()
  } catch (e) {
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

const handleRead = async (message) => {
  try {
    await markMessageRead(message.messageID)
    await Promise.all([loadMessages(), loadUnreadCount()])
  } catch (e) {
    error.value = '标记已读失败: ' + errorMessage(e)
  }
}

const handleMarkAllRead = async () => {
  if (!selectedFriend.value) return
  try {
    await markAllMessagesRead(selectedFriend.value.userID)
    await Promise.all([loadMessages(), loadUnreadCount()])
  } catch (e) {
    error.value = '标记会话已读失败: ' + errorMessage(e)
  }
}

const handleDeleteFriend = async () => {
  if (!selectedFriend.value || !window.confirm(`确定删除好友“${selectedFriend.value.username || selectedFriend.value.email}”吗？`)) return
  try {
    error.value = null
    await deleteFriend(selectedFriend.value.friendshipID)
    selectedFriend.value = null
    messages.value = []
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

watch(activeTab, async (tab) => {
  if (tab === 'messages') {
    await Promise.all([loadFriends(), loadUnreadCount()])
    await loadMessages()
  }
  if (tab === 'notifications') {
    await loadNotifications()
  }
})

onMounted(async () => {
  await Promise.all([loadFriends(), loadUnreadCount(), loadNotifications()])
})
</script>

<style scoped>
.messages-layout {
  display: grid;
  grid-template-columns: 280px minmax(0, 1fr);
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
.conversation-actions {
  display: flex;
  align-items: center;
  gap: 0.75rem;
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

.request-item {
  padding: 0.5rem 0;
}

.friend-item {
  width: 100%;
  border: none;
  background: transparent;
  border-radius: var(--radius);
  color: var(--text);
  cursor: pointer;
  padding: 0.625rem 0.75rem;
  text-align: left;
}

.friend-item:hover,
.friend-item.active {
  background: var(--bg);
  color: var(--primary);
}

.friend-item small {
  color: var(--text-secondary);
}

.message-list,
.notification-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-top: 1rem;
}

.message-item {
  display: flex;
  gap: 1rem;
  padding: 1rem;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
}

.message-item.mine {
  background: #f8fafc;
}

.message-item.unread {
  border-color: var(--primary);
}

.message-content {
  flex: 1;
  min-width: 0;
}

.message-header {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 0.5rem;
}

.message-sender {
  font-weight: 600;
}

.message-time,
.notification-time,
.empty-inline {
  color: var(--text-secondary);
  font-size: 0.75rem;
}

.message-text {
  color: var(--text-secondary);
}

.message-form {
  margin-top: 1rem;
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
}
</style>
