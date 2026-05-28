<template>
  <div class="page-container">
    <h1 class="page-title">消息中心</h1>

    <div class="tabs">
      <button 
        :class="['tab', { active: activeTab === 'messages' }]" 
        @click="activeTab = 'messages'"
      >
        私信
        <span class="badge badge-red" v-if="unreadMessages > 0">{{ unreadMessages }}</span>
      </button>
      <button 
        :class="['tab', { active: activeTab === 'notifications' }]" 
        @click="activeTab = 'notifications'"
      >
        通知
        <span class="badge badge-red" v-if="unreadNotifications > 0">{{ unreadNotifications }}</span>
      </button>
    </div>

    <div v-if="activeTab === 'messages'" class="tab-content">
      <div v-if="loading" class="loading">加载中...</div>
      <div v-else-if="messages.length === 0" class="empty-state">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z" />
          <polyline points="22,6 12,13 2,6" />
        </svg>
        <p>暂无私信</p>
      </div>
      <div v-else class="message-list">
        <div 
          v-for="message in messages" 
          :key="message.id" 
          :class="['message-item', { unread: !message.isRead }]"
        >
          <div class="message-avatar">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
              <circle cx="12" cy="7" r="4" />
            </svg>
          </div>
          <div class="message-content">
            <div class="message-header">
              <span class="message-sender">{{ message.sender }}</span>
              <span class="message-time">{{ message.time }}</span>
            </div>
            <p class="message-text">{{ message.content }}</p>
          </div>
        </div>
      </div>
    </div>

    <div v-if="activeTab === 'notifications'" class="tab-content">
      <div v-if="loading" class="loading">加载中...</div>
      <div v-else-if="notifications.length === 0" class="empty-state">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
          <path d="M13.73 21a2 2 0 0 1-3.46 0" />
        </svg>
        <p>暂无通知</p>
      </div>
      <div v-else class="notification-list">
        <div 
          v-for="notification in notifications" 
          :key="notification.id" 
          class="notification-item"
        >
          <div class="notification-icon">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="12" r="10" />
              <line x1="12" y1="8" x2="12" y2="12" />
              <line x1="12" y1="16" x2="12.01" y2="16" />
            </svg>
          </div>
          <div class="notification-content">
            <h4>{{ notification.title }}</h4>
            <p>{{ notification.content }}</p>
            <span class="notification-time">{{ notification.time }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'

const activeTab = ref('messages')
const loading = ref(false)
const unreadMessages = ref(0)
const unreadNotifications = ref(0)

const messages = ref([])
const notifications = ref([])
</script>

<style scoped>
.message-list,
.notification-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.message-item {
  display: flex;
  gap: 1rem;
  padding: 1rem;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  cursor: pointer;
  transition: all 0.2s;
}

.message-item:hover {
  background: var(--bg);
}

.message-item.unread {
  background: #f0f9ff;
  border-color: var(--primary);
}

.message-avatar {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: var(--primary);
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.message-avatar svg {
  width: 24px;
  height: 24px;
}

.message-content {
  flex: 1;
  min-width: 0;
}

.message-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.5rem;
}

.message-sender {
  font-weight: 600;
  color: var(--text);
}

.message-time {
  font-size: 0.75rem;
  color: var(--text-secondary);
}

.message-text {
  color: var(--text-secondary);
  font-size: 0.875rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.notification-item {
  display: flex;
  gap: 1rem;
  padding: 1rem;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
}

.notification-icon {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: var(--bg);
  color: var(--primary);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.notification-icon svg {
  width: 20px;
  height: 20px;
}

.notification-content {
  flex: 1;
}

.notification-content h4 {
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--text);
  margin-bottom: 0.25rem;
}

.notification-content p {
  font-size: 0.875rem;
  color: var(--text-secondary);
  margin-bottom: 0.5rem;
}

.notification-time {
  font-size: 0.75rem;
  color: var(--text-secondary);
}
</style>
