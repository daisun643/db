<template>
  <div class="page-container user-home-page">
    <MessagePopup :message="toast.message" :type="toast.type" @close="toast.message = ''" />

    <div v-if="loading" class="loading">加载中...</div>
    <div v-else-if="loadError" class="error-message">{{ loadError }}</div>
    <template v-else-if="profile">
      <section class="card user-hero">
        <div class="hero-avatar">
          <img v-if="profile.avatarUrl" :src="profile.avatarUrl" :alt="displayName" />
          <span v-else>{{ avatarInitial }}</span>
        </div>
        <div class="hero-info">
          <div class="hero-title">
            <h2>{{ displayName }}</h2>
            <span class="hero-username">@{{ profile.username || '-' }}</span>
            <span class="badge badge-level">Lv.{{ profile.userLevel || 1 }}</span>
          </div>
          <p class="hero-bio">{{ profile.bio || '这个人很懒，还没有写个人简介。' }}</p>
          <div class="hero-stats">
            <div class="hero-stat"><strong>{{ profile.postCount || 0 }}</strong><span>帖子</span></div>
            <div class="hero-stat"><strong>{{ profile.productCount || 0 }}</strong><span>商品</span></div>
            <div class="hero-stat"><strong>{{ profile.friendCount || 0 }}</strong><span>好友</span></div>
            <div class="hero-stat"><strong>{{ profile.totalCredit || 0 }}</strong><span>等级积分</span></div>
          </div>
        </div>
        <div class="hero-actions">
          <template v-if="profile.isSelf">
            <router-link to="/profile" class="btn btn-primary">信息维护</router-link>
          </template>
          <template v-else>
            <button
              v-if="relation === 'none'"
              class="btn btn-primary"
              :disabled="friendBusy"
              @click="handleAddFriend"
            >
              {{ friendBusy ? '发送中...' : '加为好友' }}
            </button>
            <button v-else-if="relation === 'friend'" class="btn friend-done" disabled>已是好友</button>
            <button v-else-if="relation === 'pending-sent'" class="btn friend-done" disabled>申请已发送</button>
            <router-link v-else-if="relation === 'pending-received'" to="/messages" class="btn">
              对方已申请，去处理
            </router-link>
          </template>
        </div>
      </section>

      <section class="user-content">
        <UserTabs v-model="activeTab" :is-self="!!profile.isSelf" />
        <UserPostsPanel
          v-if="activeTab === 'posts'"
          :user-id="userId"
          @error="toast = { message: $event, type: 'error' }"
        />
        <UserProductsPanel
          v-else-if="activeTab === 'products'"
          :user-id="userId"
          @error="toast = { message: $event, type: 'error' }"
        />
      </section>
    </template>
  </div>
</template>

<script setup>
import { computed, reactive, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { createFriendRequest, getFriendRelation, getUserPublicProfile } from '../api'
import MessagePopup from '../components/MessagePopup.vue'
import UserTabs from '../tab/user/UserTabs.vue'
import UserPostsPanel from '../tab/user/UserPostsPanel.vue'
import UserProductsPanel from '../tab/user/UserProductsPanel.vue'

const route = useRoute()

const userId = computed(() => Number(route.params.id))
const profile = ref(null)
const relation = ref('none')
const loading = ref(false)
const loadError = ref('')
const friendBusy = ref(false)
const activeTab = ref('posts')
const toast = reactive({ message: '', type: 'success' })

const displayName = computed(() =>
  profile.value?.username || '校园用户'
)

const avatarInitial = computed(() =>
  (displayName.value || '校')[0]?.toUpperCase() || '校'
)

const loadProfile = async () => {
  loading.value = true
  loadError.value = ''
  try {
    const [profileRes, relationRes] = await Promise.all([
      getUserPublicProfile(userId.value),
      getFriendRelation(userId.value),
    ])
    profile.value = profileRes.data
    relation.value = relationRes.data?.relation || 'none'
  } catch (e) {
    profile.value = null
    loadError.value = e.response?.status === 404
      ? '该用户不存在或已注销'
      : '加载用户主页失败: ' + (e.response?.data?.message || e.message)
  } finally {
    loading.value = false
  }
}

const handleAddFriend = async () => {
  try {
    friendBusy.value = true
    await createFriendRequest({ userId: userId.value })
    relation.value = 'pending-sent'
    toast.message = '好友申请已发送，等待对方处理。'
    toast.type = 'success'
  } catch (e) {
    toast.message = '好友申请发送失败: ' + (e.response?.data?.message || e.message)
    toast.type = 'error'
    await loadRelationQuietly()
  } finally {
    friendBusy.value = false
  }
}

const loadRelationQuietly = async () => {
  try {
    const res = await getFriendRelation(userId.value)
    relation.value = res.data?.relation || 'none'
  } catch {
    // 静默失败，保留当前状态
  }
}

watch(userId, () => {
  activeTab.value = 'posts'
  if (userId.value) loadProfile()
}, { immediate: true })
</script>

<style scoped>
.user-home-page { width: 100%; max-width: 1080px; margin: 0 auto; padding-top: 1.25rem; display: grid; gap: 1rem; }
.user-hero { display: flex; align-items: flex-start; gap: 1.2rem; padding: 1.6rem; }
.hero-avatar { width: 84px; height: 84px; flex: 0 0 auto; border-radius: 50%; overflow: hidden; display: grid; place-items:center; color: #fff; background: linear-gradient(135deg, #5f50dc, #8b6be8); font-size: 1.9rem; font-weight: 800; }
.hero-avatar img { width: 100%; height: 100%; object-fit: cover; }
.hero-info { min-width: 0; flex: 1; }
.hero-title { display: flex; align-items: center; flex-wrap: wrap; gap: .55rem; }
.hero-title h2 { margin: 0; font-size: 1.25rem; color: #172033; }
.hero-username { color: var(--text-secondary); font-size: .82rem; }
.badge-level { background: #efedff; color: #5749c8; }
.hero-bio { margin: .5rem 0 0; color: var(--text-secondary); font-size: .85rem; line-height: 1.6; }
.hero-stats { display: flex; flex-wrap: wrap; gap: 1.4rem; margin-top: .9rem; }
.hero-stat { display: grid; justify-items: start; }
.hero-stat strong { color: #1d2438; font-size: 1rem; }
.hero-stat span { color: #8a90a1; font-size: .72rem; }
.hero-actions { flex: 0 0 auto; display: flex; flex-direction: column; gap: .5rem; }
.hero-actions .btn { text-decoration: none; justify-content: center; }
.friend-done { background: #eef0f4; color: #8a90a1; cursor: default; }
.user-content { display: grid; gap: 1rem; }
@media (max-width: 640px) {
  .user-hero { flex-direction: column; align-items: stretch; }
  .hero-actions .btn { width: 100%; }
}
</style>
