<template>
  <div class="tab-content managed-forums">
    <div class="managed-forums-toolbar">
      <button class="create-forum-trigger" type="button" @click="openForumCreator">＋ 创建版块</button>
    </div>
    <div v-if="loadingMyForums" class="loading">加载中...</div>
    <div v-else-if="myForums.length" class="managed-forum-list">
      <form
        v-for="forum in myForums"
        :key="forum.forumID"
        class="managed-forum-card"
        @submit.prevent="handleUpdateForum(forum)"
      >
        <div class="managed-forum-heading">
          <div>
            <span class="composer-kicker">版块 #{{ forum.forumID }}</span>
            <strong>{{ forum.forumName }}</strong>
          </div>
          <span :class="['badge', forum.status === 'Active' ? 'badge-green' : 'badge-yellow']">
            {{ forum.status === 'Active' ? '开放中' : '已停用' }}
          </span>
        </div>
        <label>
          版块名称
          <input v-model="forum.forumName" type="text" minlength="2" maxlength="100" required />
        </label>
        <label>
          版块描述
          <textarea v-model="forum.description" maxlength="500" rows="4"></textarea>
        </label>
        <label>
          开放状态
          <select v-model="forum.status">
            <option value="Active">开放</option>
            <option value="Inactive">停用</option>
          </select>
        </label>
        <div class="managed-forum-meta">
          <span>{{ forum.postCount || 0 }} 篇公开帖子</span>
          <span>版主：{{ managerNames(forum) }}</span>
        </div>
        <button class="btn btn-primary" type="submit" :disabled="savingForumId === forum.forumID">
          {{ savingForumId === forum.forumID ? '保存中...' : '保存设置' }}
        </button>
      </form>
    </div>
    <div v-else class="empty-state">
      <p>暂无负责的版块</p>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { useForum } from '../../composables/useForum'
import { getMyForums, updateForum } from '../../api'

const { error, notice, loadForums, openForumCreator } = useForum()

const myForums = ref([])
const loadingMyForums = ref(false)
const savingForumId = ref(null)

const managerNames = (forum) => (forum.managers || [])
  .map(manager => manager.username || manager.email)
  .filter(Boolean)
  .join('、') || '暂无'

const loadMyForums = async () => {
  try {
    loadingMyForums.value = true
    const res = await getMyForums()
    myForums.value = Array.isArray(res.data) ? res.data : []
  } catch (e) {
    myForums.value = []
    error.value = '无法加载我的版块: ' + (e.response?.data?.message || e.message)
  } finally {
    loadingMyForums.value = false
  }
}

const handleUpdateForum = async (forum) => {
  try {
    savingForumId.value = forum.forumID
    const res = await updateForum(forum.forumID, {
      forumName: forum.forumName.trim(),
      description: (forum.description || '').trim(),
      status: forum.status,
    })
    Object.assign(forum, res.data)
    notice.value = '版块设置已保存。'
    await loadForums()
  } catch (e) {
    error.value = '保存版块失败: ' + (e.response?.data?.message || e.message)
  } finally {
    savingForumId.value = null
  }
}

onMounted(() => {
  loadMyForums()
})
</script>

<style scoped>
.tab-content {
  min-height: 400px;
  min-width: 0;
}

.managed-forums {
  width: min(920px, 100%);
  margin: 0 auto;
}

.managed-forums-toolbar {
  display: flex;
  justify-content: flex-end;
  margin-bottom: .85rem;
}

.create-forum-trigger {
  background: #2f7ee0;
  border: 0;
  border-radius: 4px;
  color: #fff;
  cursor: pointer;
  padding: .55rem .9rem;
  font: inherit;
  font-size: .8rem;
  font-weight: 700;
  white-space: nowrap;
}

.create-forum-trigger:hover {
  background: #266bc4;
}

.managed-forum-list {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 1rem;
}

.managed-forum-card {
  display: grid;
  gap: .85rem;
  padding: 1.15rem;
  border: 1px solid var(--border);
  border-radius: 14px;
  background: #fff;
}

.managed-forum-heading {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
}

.managed-forum-heading strong {
  display: block;
  margin-top: .25rem;
  color: #242a3c;
  font-size: 1.05rem;
}

.managed-forum-card label {
  display: grid;
  gap: .4rem;
  color: var(--text-secondary);
  font-size: .78rem;
}

.managed-forum-card :is(input, textarea, select) {
  width: 100%;
  padding: .7rem .75rem;
  border: 1px solid #dfe2e8;
  border-radius: 9px;
  background: #fff;
  color: var(--text);
  font: inherit;
}

.managed-forum-card textarea {
  resize: vertical;
}

.managed-forum-meta {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: .5rem 1rem;
  color: var(--text-secondary);
  font-size: .75rem;
}

.managed-forum-card .btn {
  justify-self: end;
}

.composer-kicker {
  display: block;
  margin-bottom: .25rem;
  color: var(--primary);
  font-size: .66rem;
  font-weight: 750;
  letter-spacing: .08em;
}

.empty-state {
  padding: 3rem 1rem;
  color: var(--text-secondary);
  text-align: center;
  border: 1px dashed #d9dbe5;
  border-radius: 20px;
  background: #fafaff;
}
</style>
