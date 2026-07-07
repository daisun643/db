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
          <span class="badge badge-red" v-if="unreadNotifications > 0">{{ unreadNotifications }}</span>
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
            <button class="btn" @click="handleMarkAllRead">全部已读</button>
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
      <div class="notification-toolbar">
        <select v-model="notificationType" @change="resetAndLoadNotifications">
          <option value="">全部类型</option>
          <option value="Mention">@提及</option>
          <option value="Transaction">交易</option>
          <option value="Audit">审核</option>
          <option value="Report">举报</option>
          <option value="Dispute">纠纷</option>
          <option value="System">系统</option>
        </select>
        <select v-model="notificationReadFilter" @change="resetAndLoadNotifications">
          <option value="">全部状态</option>
          <option value="false">未读</option>
          <option value="true">已读</option>
        </select>
        <button class="btn" @click="handleMarkAllNotificationsRead" :disabled="unreadNotifications === 0">全部已读</button>
      </div>

      <div v-if="loading" class="loading">加载中...</div>
      <div v-else-if="notifications.length === 0" class="empty-state">
        <p>暂无通知</p>
      </div>
      <div v-else class="notification-list">
        <article
          v-for="notification in notifications"
          :key="notification.notificationID"
          :class="['notification-item', { unread: !notification.isRead }]"
        >
          <div class="notification-icon">{{ typeIcon(notification.type) }}</div>
          <div class="notification-content">
            <div class="notification-heading">
              <h4>{{ notification.title }}</h4>
              <span class="notification-type">{{ notification.type || 'System' }}</span>
            </div>
            <p>{{ notification.content }}</p>
            <span class="notification-time">{{ formatDate(notification.createTime) }}</span>
          </div>
          <div class="notification-actions">
            <button v-if="!notification.isRead" class="link-button" @click="handleReadNotification(notification)">标为已读</button>
            <button class="link-button danger" @click="handleDeleteNotification(notification)">删除</button>
          </div>
        </article>
      </div>

      <div class="pagination-bar" v-if="notificationTotal > notificationPageSize">
        <button class="btn" :disabled="notificationPage <= 1" @click="changeNotificationPage(notificationPage - 1)">上一页</button>
        <span>第 {{ notificationPage }} 页 / 共 {{ notificationTotalPages }} 页</span>
        <button class="btn" :disabled="notificationPage >= notificationTotalPages" @click="changeNotificationPage(notificationPage + 1)">下一页</button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import {
  acceptFriendRequest,
  createFriendRequest,
  deleteNotification,
  getFriendRequests,
  getFriends,
  getMessages,
  getNotifications,
  getUnreadMessageCount,
  getUnreadNotificationCount,
  markAllMessagesRead,
  markAllNotificationsRead,
  markMessageRead,
  markNotificationRead,
  rejectFriendRequest,
  sendMessage,
} from '../api'

const activeTab = ref('messages')
const loading = ref(false)
const unreadMessages = ref(0)
const unreadNotifications = ref(0)
const friends = ref([])
const friendRequests = ref([])
const messages = ref([])
const notifications = ref([])
const selectedFriend = ref(null)
const friendEmail = ref('')
const messageText = ref('')
const error = ref(null)
const notificationType = ref('')
const notificationReadFilter = ref('')
const notificationPage = ref(1)
const notificationPageSize = ref(20)
const notificationTotal = ref(0)

const notificationTotalPages = computed(() => Math.max(1, Math.ceil(notificationTotal.value / notificationPageSize.value)))

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
    messages.value = res.data.reverse()
  } catch (e) {
    error.value = '无法加载私信: ' + (e.response?.data?.message || e.message)
  } finally {
    loading.value = false
  }
}

const loadNotifications = async () => {
  try {
    loading.value = true
    const params = {
      page: notificationPage.value,
      pageSize: notificationPageSize.value,
    }
    if (notificationType.value) params.type = notificationType.value
    if (notificationReadFilter.value !== '') params.isRead = notificationReadFilter.value

    const res = await getNotifications(params)
    notifications.value = res.data.items || res.data
    notificationTotal.value = res.data.total ?? notifications.value.length
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

const loadUnreadNotifications = async () => {
  const res = await getUnreadNotificationCount()
  unreadNotifications.value = res.data.count || 0
}

const resetAndLoadNotifications = async () => {
  notificationPage.value = 1
  await loadNotifications()
}

const changeNotificationPage = async (page) => {
  notificationPage.value = page
  await loadNotifications()
}

const selectFriend = async (friend) => {
  selectedFriend.value = friend
  await loadMessages()
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
  await acceptFriendRequest(request.friendshipID)
  await loadFriends()
}

const handleReject = async (request) => {
  await rejectFriendRequest(request.friendshipID)
  await loadFriends()
}

const handleSendMessage = async () => {
  if (!selectedFriend.value) return
  await sendMessage({
    receiverID: selectedFriend.value.userID,
    content: messageText.value,
  })
  messageText.value = ''
  await loadMessages()
}

const handleRead = async (message) => {
  await markMessageRead(message.messageID)
  await Promise.all([loadMessages(), loadUnreadCount()])
}

const handleMarkAllRead = async () => {
  await markAllMessagesRead()
  await Promise.all([loadMessages(), loadUnreadCount()])
}

const handleReadNotification = async (notification) => {
  await markNotificationRead(notification.notificationID)
  await Promise.all([loadNotifications(), loadUnreadNotifications()])
}

const handleMarkAllNotificationsRead = async () => {
  await markAllNotificationsRead()
  await Promise.all([loadNotifications(), loadUnreadNotifications()])
}

const handleDeleteNotification = async (notification) => {
  await deleteNotification(notification.notificationID)
  await Promise.all([loadNotifications(), loadUnreadNotifications()])
}

const typeIcon = (type) => {
  const map = { System: '系', Mention: '@', Transaction: '交', Audit: '审', Report: '举', Dispute: '纠' }
  return map[type] || (type?.slice(0, 1) || '!')
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
  error.value = null
  if (tab === 'messages') {
    await Promise.all([loadFriends(), loadUnreadCount()])
    await loadMessages()
  }
  if (tab === 'notifications') {
    await Promise.all([loadNotifications(), loadUnreadNotifications()])
  }
})

onMounted(async () => {
  await Promise.all([loadFriends(), loadUnreadCount(), loadNotifications(), loadUnreadNotifications()])
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
.message-form,
.notification-toolbar,
.pagination-bar {
  display: flex;
  gap: 0.5rem;
}

.notification-toolbar,
.pagination-bar {
  align-items: center;
  flex-wrap: wrap;
}

.friend-form input,
.message-form input,
.notification-toolbar select {
  min-width: 0;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 0.625rem 0.75rem;
  font: inherit;
}

.friend-form input,
.message-form input {
  flex: 1;
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

.message-item,
.notification-item {
  display: flex;
  gap: 1rem;
  padding: 1rem;
}

.message-item.mine {
  background: #f8fafc;
}

.message-item.unread,
.notification-item.unread {
  border-color: var(--primary);
}

.message-content,
.notification-content {
  flex: 1;
  min-width: 0;
}

.message-header,
.notification-heading {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 0.5rem;
}

.message-sender,
.notification-heading h4 {
  font-weight: 600;
}

.message-time,
.notification-time,
.empty-inline,
.notification-type {
  color: var(--text-secondary);
  font-size: 0.75rem;
}

.message-text,
.notification-content p {
  color: var(--text-secondary);
}

.message-form,
.pagination-bar {
  margin-top: 1rem;
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

.notification-actions {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
  align-items: flex-end;
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
  .conversation-header,
  .notification-item,
  .notification-actions {
    align-items: stretch;
    flex-direction: column;
  }
}
</style>
