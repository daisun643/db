<template>
  <div class="page-container">
    <h1 class="page-title">首页</h1>

    <div class="card">
      <h2 style="margin-bottom: 1rem;">系统公告</h2>
      <div v-if="loading" class="loading">加载中...</div>
      <div v-else-if="error" class="error-message">{{ error }}</div>
      <div v-else-if="announcements.length === 0" style="color: var(--text-secondary); text-align: center; padding: 2rem;">
        暂无公告
      </div>
      <div v-else class="announcements">
        <div v-for="announcement in announcements" :key="announcement.id" class="announcement-item">
          <h3>{{ announcement.title }}</h3>
          <p>{{ announcement.content }}</p>
          <span class="announcement-date">{{ announcement.date }}</span>
        </div>
      </div>
    </div>

    <div class="card">
      <h2 style="margin-bottom: 1rem;">关于我们</h2>
      <div class="about-content">
        <p>欢迎来到同济论坛！</p>
        <p>这是一个面向同济大学师生的交流平台，提供论坛讨论、闲置物品交易等功能。</p>
        <p>我们致力于为同济师生打造一个安全、便捷、友好的线上社区。</p>
        <div class="contact-info">
          <h3>联系方式</h3>
          <p>邮箱: 2352580@tongji.edu.cn</p>
          <p>地址: 上海市杨浦区四平路1239号</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'

const announcements = ref([])
const loading = ref(true)
const error = ref(null)

onMounted(async () => {
  try {
    loading.value = false
  } catch (e) {
    error.value = '无法加载公告数据'
    loading.value = false
  }
})
</script>

<style scoped>
.announcements {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.announcement-item {
  padding: 1rem;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: var(--bg);
}

.announcement-item h3 {
  font-size: 1.125rem;
  font-weight: 600;
  margin-bottom: 0.5rem;
  color: var(--text);
}

.announcement-item p {
  color: var(--text-secondary);
  line-height: 1.6;
  margin-bottom: 0.5rem;
}

.announcement-date {
  font-size: 0.875rem;
  color: var(--text-secondary);
}

.about-content {
  line-height: 1.8;
}

.about-content p {
  margin-bottom: 1rem;
  color: var(--text);
}

.contact-info {
  margin-top: 2rem;
  padding-top: 1.5rem;
  border-top: 1px solid var(--border);
}

.contact-info h3 {
  font-size: 1.125rem;
  font-weight: 600;
  margin-bottom: 1rem;
  color: var(--text);
}

.contact-info p {
  margin-bottom: 0.5rem;
  color: var(--text-secondary);
}
</style>
