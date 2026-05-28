<template>
  <div>
    <h1 class="page-title">系统状态</h1>

    <div class="stat-cards">
      <div class="stat-card">
        <div class="number">{{ stats.users }}</div>
        <div class="label">用户总数</div>
      </div>
      <div class="stat-card">
        <div class="number">{{ stats.forums }}</div>
        <div class="label">论坛数量</div>
      </div>
      <div class="stat-card">
        <div class="number">{{ stats.posts }}</div>
        <div class="label">帖子数量</div>
      </div>
      <div class="stat-card">
        <div class="number">{{ stats.products }}</div>
        <div class="label">商品数量</div>
      </div>
    </div>

    <div class="card">
      <h2 style="margin-bottom: 1rem;">数据库连接状态</h2>
      <div v-if="loading" class="loading">正在检查系统状态...</div>
      <div v-else-if="health" :class="['badge', health.status === 'healthy' ? 'badge-green' : 'badge-red']">
        {{ health.status === 'healthy' ? '数据库连接正常' : '数据库连接异常' }}
      </div>
      <div v-if="health && health.database" style="margin-top: 1rem; color: var(--text-secondary);">
        数据库: {{ health.database }}
      </div>
    </div>

    <div class="card">
      <h2 style="margin-bottom: 1rem;">用户管理</h2>
      <div v-if="error" class="error-message">{{ error }}</div>
      <div v-else-if="loadingUsers" class="loading">加载中...</div>
      <div v-else class="table-wrapper">
        <table>
          <thead>
            <tr>
              <th>ID</th>
              <th>用户名</th>
              <th>邮箱</th>
              <th>用户代码</th>
              <th>信用分</th>
              <th>状态</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="user in users" :key="user.userID">
              <td>{{ user.userID }}</td>
              <td>{{ user.username }}</td>
              <td>{{ user.email }}</td>
              <td>{{ user.userCode }}</td>
              <td>{{ user.credit }}</td>
              <td>
                <span :class="['badge', user.status === 'Active' ? 'badge-green' : 'badge-yellow']">
                  {{ user.status || '未知' }}
                </span>
              </td>
            </tr>
            <tr v-if="users.length === 0">
              <td colspan="6" style="text-align: center; color: var(--text-secondary);">暂无数据</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { getUsers, getForums, getPosts, getProducts, getHealth } from '../api'

const stats = ref({ users: '-', forums: '-', posts: '-', products: '-' })
const health = ref(null)
const users = ref([])
const loading = ref(true)
const loadingUsers = ref(true)
const error = ref(null)

onMounted(async () => {
  try {
    const [usersRes, forums, posts, products, healthRes] = await Promise.allSettled([
      getUsers(),
      getForums(),
      getPosts(),
      getProducts(),
      getHealth(),
    ])
    
    stats.value = {
      users: usersRes.status === 'fulfilled' ? usersRes.value.data.length : 0,
      forums: forums.status === 'fulfilled' ? forums.value.data.length : 0,
      posts: posts.status === 'fulfilled' ? posts.value.data.length : 0,
      products: products.status === 'fulfilled' ? products.value.data.length : 0,
    }
    
    health.value = healthRes.status === 'fulfilled' ? healthRes.value.data : { status: 'unhealthy' }
    
    if (usersRes.status === 'fulfilled') {
      users.value = usersRes.value.data
    }
  } catch (e) {
    error.value = '无法加载系统数据: ' + (e.response?.data?.message || e.message)
    health.value = { status: 'unhealthy' }
  } finally {
    loading.value = false
    loadingUsers.value = false
  }
})
</script>
