<template>
  <div class="page-container">
    <h1 class="page-title">论坛</h1>

    <div class="tabs">
      <button 
        :class="['tab', { active: activeTab === 'all' }]" 
        @click="activeTab = 'all'"
      >
        所有论坛
      </button>
      <button 
        :class="['tab', { active: activeTab === 'my-posts' }]" 
        @click="activeTab = 'my-posts'"
      >
        我的帖子
      </button>
      <button 
        :class="['tab', { active: activeTab === 'favorites' }]" 
        @click="activeTab = 'favorites'"
      >
        收藏夹
      </button>
    </div>

    <div v-if="error" class="error-message">{{ error }}</div>
    <div v-if="loading" class="loading">加载中...</div>

    <div v-else-if="activeTab === 'all'" class="grid">
      <div class="card" v-for="forum in forums" :key="forum.forumID">
        <h3 style="margin-bottom: 0.5rem;">{{ forum.forumName }}</h3>
        <p style="color: var(--text-secondary); font-size: 0.875rem; margin-bottom: 0.75rem;">
          {{ forum.description || '暂无描述' }}
        </p>
        <span :class="['badge', forum.status === 'Active' ? 'badge-green' : 'badge-yellow']">
          {{ forum.status || '未知' }}
        </span>
      </div>
      <div v-if="forums.length === 0" class="card" style="text-align: center; color: var(--text-secondary);">
        暂无论坛
      </div>
    </div>

    <div v-else-if="activeTab === 'my-posts'" class="tab-content">
      <div class="empty-state">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
          <polyline points="14 2 14 8 20 8" />
          <line x1="16" y1="13" x2="8" y2="13" />
          <line x1="16" y1="17" x2="8" y2="17" />
          <polyline points="10 9 9 9 8 9" />
        </svg>
        <p>暂无帖子</p>
      </div>
    </div>

    <div v-else-if="activeTab === 'favorites'" class="tab-content">
      <div class="empty-state">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
        </svg>
        <p>暂无收藏</p>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { getForums } from '../api'

const activeTab = ref('all')
const forums = ref([])
const loading = ref(true)
const error = ref(null)

onMounted(async () => {
  try {
    const res = await getForums()
    forums.value = res.data
  } catch (e) {
    error.value = '无法加载论坛数据: ' + (e.response?.data?.message || e.message)
  } finally {
    loading.value = false
  }
})
</script>

