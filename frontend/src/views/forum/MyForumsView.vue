<template>
  <div class="tab-content managed-forums">
    <header class="mf-page-head">
      <div>
        <h2 class="mf-page-title">我管理的版块</h2>
        <p class="mf-page-sub">
          管理你负责的版块：编辑基本信息、更换头像、维护管理团队（版主/管理员）、置顶加精版块内帖子。
        </p>
      </div>
      <button class="mf-create-btn" type="button" @click="openForumCreator">
        ＋ 创建我的版块
      </button>
    </header>

    <div v-if="loadingMyForums" class="loading">加载中...</div>

    <div v-else-if="myForums.length" class="mf-list">
      <article
        v-for="forum in myForums"
        :key="forum.forumID"
        class="mf-card"
      >
        <!-- 版块头部：身份与概览 -->
        <header class="mf-head">
          <img
            v-if="canShowAvatar(forum.avatarUrl)"
            :src="forum.avatarUrl"
            :alt="forum.forumName + ' 版块头像'"
            class="mf-avatar"
            @error="markAvatarFailed(forum.avatarUrl)"
          />
          <span v-else class="mf-avatar mf-avatar-fallback" aria-hidden="true">
            {{ (forum.forumName || '版')[0] }}
          </span>

          <div class="mf-head-main">
            <div class="mf-name-row">
              <h3 class="mf-name">{{ forum.forumName }}</h3>
              <span :class="['mf-pill', forum.status === 'Active' ? 'mf-pill-on' : 'mf-pill-off']">
                <i class="mf-pill-dot" aria-hidden="true"></i>
                {{ forum.status === 'Active' ? '开放中' : '已停用' }}
              </span>
            </div>
            <p class="mf-desc">{{ forum.description || '这个版块还没有简介' }}</p>
            <span class="mf-id">版块 #{{ forum.forumID }}</span>
          </div>

          <dl class="mf-stats">
            <div class="mf-stat">
              <dt>帖子</dt>
              <dd>{{ forum.postCount || 0 }}</dd>
            </div>
            <div class="mf-stat">
              <dt>关注</dt>
              <dd>{{ forum.memberCount || 0 }}</dd>
            </div>
            <div class="mf-stat">
              <dt>管理团队</dt>
              <dd>{{ teamOf(forum).length }}</dd>
            </div>
          </dl>

          <button
            class="mf-toggle"
            type="button"
            :aria-expanded="!!expanded[forum.forumID]"
            @click="togglePanel(forum)"
          >{{ expanded[forum.forumID] ? '收起管理面板 ▴' : '展开管理面板 ▾' }}</button>
        </header>

        <div v-show="expanded[forum.forumID]" class="mf-body">
        <div class="mf-grid">
          <!-- 左栏：版块设置 -->
          <form class="mf-panel" @submit.prevent="handleUpdateForum(forum)">
            <h4 class="mf-panel-title">版块设置</h4>

            <div class="mf-field-row">
              <label class="mf-field">
                <span class="mf-label">版块名称</span>
                <input v-model="forum.forumName" type="text" minlength="2" maxlength="100" required />
              </label>
              <label class="mf-field">
                <span class="mf-label">开放状态</span>
                <select v-model="forum.status">
                  <option value="Active">开放</option>
                  <option value="Inactive">停用</option>
                </select>
              </label>
            </div>

            <label class="mf-field">
              <span class="mf-label">
                版块描述
                <small class="mf-counter">{{ (forum.description || '').length }}/500</small>
              </span>
              <textarea v-model="forum.description" maxlength="500" rows="3" placeholder="介绍一下这个版块的主题和氛围"></textarea>
            </label>

            <div class="mf-avatar-row">
              <span class="mf-label">版块头像</span>
              <div class="mf-avatar-actions">
                <input
                  :ref="el => avatarInputs[forum.forumID] = el"
                  type="file"
                  accept="image/png, image/jpeg, image/webp, image/gif"
                  class="visually-hidden-input"
                  @change="handleAvatarPick(forum, $event)"
                />
                <button
                  class="mf-btn"
                  type="button"
                  :disabled="uploadingAvatarId === forum.forumID"
                  @click="avatarInputs[forum.forumID]?.click()"
                >{{ uploadingAvatarId === forum.forumID ? '上传中…' : (forum.avatarUrl ? '更换头像' : '上传头像') }}</button>
                <button
                  v-if="forum.avatarUrl"
                  class="mf-btn mf-btn-ghost"
                  type="button"
                  :disabled="uploadingAvatarId === forum.forumID"
                  @click="handleRemoveAvatar(forum)"
                >移除</button>
                <span class="mf-hint">jpg / png / webp / gif，不超过 2MB</span>
              </div>
            </div>

            <div class="mf-panel-foot">
              <button
                class="mf-btn mf-btn-primary"
                type="submit"
                :disabled="savingForumId === forum.forumID"
              >{{ savingForumId === forum.forumID ? '保存中…' : '保存设置' }}</button>
            </div>
          </form>

          <!-- 右栏：管理团队（版主/管理员） -->
          <section class="mf-panel">
            <h4 class="mf-panel-title">
              管理团队
              <span class="mf-panel-count">{{ moderatorCount(forum) }} 位版主 · {{ adminCount(forum) }} 位管理员</span>
            </h4>

            <ul v-if="teamOf(forum).length" class="mf-manager-list">
              <li
                v-for="manager in teamOf(forum)"
                :key="manager.userID"
                class="mf-manager-item"
              >
                <span class="mf-manager-avatar" aria-hidden="true">
                  {{ (manager.username || manager.email || '?')[0] }}
                </span>
                <span class="mf-manager-info">
                  <b>
                    {{ manager.username || manager.email }}
                    <span :class="['mf-role-badge', manager.role === 'Admin' ? 'mf-role-admin' : 'mf-role-moderator']">
                      {{ manager.role === 'Admin' ? '管理员' : '版主' }}
                    </span>
                    <span v-if="manager.isCreator" class="mf-role-badge mf-role-creator">创建者</span>
                  </b>
                  <small v-if="manager.username && manager.email">{{ manager.email }}</small>
                </span>
                <button
                  v-if="!manager.isCreator"
                  class="mf-btn mf-btn-danger"
                  type="button"
                  :disabled="draft(forum).removingId === manager.userID"
                  :title="'移除' + (manager.role === 'Admin' ? '管理员' : '版主') + ' ' + (manager.username || manager.email)"
                  @click="handleRemoveManager(forum, manager)"
                >{{ draft(forum).removingId === manager.userID ? '移除中…' : '移除' }}</button>
              </li>
            </ul>
            <p v-else class="mf-empty-note">还没有管理人员，在下方搜索用户并指派。</p>

            <div v-if="forum.canAssignManagers" class="mf-assign">
              <span class="mf-label">添加管理人员</span>
              <div class="mf-search-row">
                <input
                  v-model="draft(forum).keyword"
                  type="text"
                  placeholder="输入用户名或邮箱"
                  aria-label="搜索要指派的用户"
                  @keyup.enter="handleSearchUsers(forum)"
                />
                <select v-model="draft(forum).assignRole" class="mf-role-select" aria-label="选择管理人员角色">
                  <option value="Admin">管理员</option>
                  <option value="Moderator">版主</option>
                </select>
                <button
                  class="mf-btn"
                  type="button"
                  :disabled="draft(forum).searching || !draft(forum).keyword.trim()"
                  @click="handleSearchUsers(forum)"
                >{{ draft(forum).searching ? '搜索中…' : '搜索' }}</button>
              </div>

              <div v-if="draft(forum).searched" class="mf-results" aria-live="polite">
                <div
                  v-for="user in draft(forum).results"
                  :key="user.userID"
                  class="mf-result-item"
                >
                  <span class="mf-manager-avatar" aria-hidden="true">
                    {{ (user.username || user.email || '?')[0] }}
                  </span>
                  <span class="mf-manager-info">
                    <b>{{ user.username }}</b>
                    <small>{{ user.email }}</small>
                  </span>
                  <button
                    class="mf-btn mf-btn-primary mf-btn-sm"
                    type="button"
                    :disabled="draft(forum).assigningId === user.userID"
                    @click="handleAssignManager(forum, user)"
                  >{{ draft(forum).assigningId === user.userID ? '指派中…' : '指派' }}</button>
                </div>
                <p v-if="!draft(forum).results.length" class="mf-empty-note">
                  未找到匹配的用户，换个关键词试试。
                </p>
              </div>
            </div>
            <p v-else class="mf-assign-hint">
              仅版主（或站点管理员）可以添加管理员等管理人员，如需调整管理团队请联系版主。
            </p>
          </section>
        </div>

        <!-- 帖子管理：置顶、加精等版块内帖子管理 -->
        <section class="mf-panel mf-posts-panel">
          <h4 class="mf-panel-title">
            帖子管理
            <span class="mf-panel-count">置顶 · 加精 · 封禁 · 审核</span>
          </h4>

          <div class="mf-post-tabs" role="tablist">
            <button
              v-for="tab in POST_TABS"
              :key="tab.value || 'public'"
              type="button"
              :class="{ active: postDraft(forum).tab === tab.value }"
              @click="switchPostTab(forum, tab.value)"
            >{{ tab.label }}</button>
          </div>

          <div v-if="postDraft(forum).loading" class="loading">加载中...</div>

          <ul v-else-if="postDraft(forum).posts.length" class="mf-post-list">
            <li
              v-for="post in postDraft(forum).posts"
              :key="post.postID"
              class="mf-post-item"
            >
              <span v-if="post.status === 'Pinned'" class="mf-post-tag mf-post-tag-pinned">置顶</span>
              <span v-else-if="post.status === 'Elite'" class="mf-post-tag mf-post-tag-elite">精</span>
              <span v-else-if="post.status === 'PendingReview'" class="mf-post-tag mf-post-tag-review">待审核</span>
              <span v-else-if="post.status === 'Banned'" class="mf-post-tag mf-post-tag-banned">已封禁</span>
              <span class="mf-post-title" :title="post.title">{{ post.title }}</span>
              <small class="mf-post-meta">{{ post.username || '校园用户' }} · {{ formatDate(post.createTime) }}</small>
              <span class="mf-post-actions">
                <button
                  v-for="action in postActions(post)"
                  :key="action.value"
                  class="mf-btn mf-btn-sm"
                  :class="{ 'mf-btn-primary': action.primary, 'mf-post-danger': action.danger }"
                  type="button"
                  :disabled="postDraft(forum).actingId === post.postID"
                  @click="handlePostAction(forum, post, action.value)"
                >{{ action.label }}</button>
              </span>
            </li>
          </ul>
          <p v-else class="mf-empty-note">该版块下暂无符合条件的帖子。</p>
        </section>
        </div>
      </article>
    </div>

    <div v-else class="empty-state mf-empty">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" aria-hidden="true">
        <rect x="3" y="4" width="18" height="16" rx="2" />
        <path d="M3 9h18M9 9v11" />
      </svg>
      <p>你还没有负责的版块</p>
      <button class="mf-btn mf-btn-primary" type="button" @click="openForumCreator">
        ＋ 创建第一个版块
      </button>
    </div>
  </div>
</template>

<script setup>
import { onMounted, reactive, ref } from 'vue'
import { useForum } from '../../composables/useForum'
import {
  assignForumManager,
  changePostStatus,
  getMyForums,
  getPosts,
  removeForumAvatar,
  removeForumManager,
  searchUsers,
  updateForum,
  uploadForumAvatar,
} from '../../api'
import { canShowAvatar, markAvatarFailed } from '../../utils/avatarFallback'

const { error, notice, loadForums, openForumCreator } = useForum()

const myForums = ref([])
const loadingMyForums = ref(false)
const savingForumId = ref(null)
const uploadingAvatarId = ref(null)
const avatarInputs = reactive({})

// 管理面板默认收缩，点击卡片头部按钮后展开
const expanded = reactive({})

// 帖子管理状态页签：空字符串表示公开可见帖子
const POST_TABS = [
  { label: '公开帖子', value: '' },
  { label: '待审核', value: 'PendingReview' },
  { label: '已封禁', value: 'Banned' },
]

// 每个版块独立的版主搜索 / 指派状态
const managerDrafts = reactive({})
const draft = (forum) => {
  if (!managerDrafts[forum.forumID]) {
    managerDrafts[forum.forumID] = {
      keyword: '',
      searched: false,
      results: [],
      searching: false,
      assigningId: null,
      removingId: null,
      assignRole: 'Admin',
    }
  }
  return managerDrafts[forum.forumID]
}

// 每个版块独立的帖子管理状态（懒加载：首次展开面板时才拉取）
const postDrafts = reactive({})
const postDraft = (forum) => {
  if (!postDrafts[forum.forumID]) {
    postDrafts[forum.forumID] = {
      loaded: false,
      loading: false,
      tab: '',
      posts: [],
      actingId: null,
    }
  }
  return postDrafts[forum.forumID]
}

const togglePanel = (forum) => {
  expanded[forum.forumID] = !expanded[forum.forumID]
  if (expanded[forum.forumID] && !postDraft(forum).loaded) {
    loadForumPosts(forum)
  }
}

// 管理团队 = 已指派管理人员 + 创建者（创建者是默认版主，不在列表中时补齐展示）
const teamOf = (forum) => {
  const team = (forum.managers || []).map(manager => ({
    ...manager,
    role: manager.role === 'Admin' ? 'Admin' : 'Moderator',
    isCreator: !!forum.creator && forum.creator.userID === manager.userID,
  }))
  if (forum.creator && !team.some(member => member.userID === forum.creator.userID)) {
    team.unshift({
      userID: forum.creator.userID,
      username: forum.creator.username,
      email: forum.creator.email,
      role: 'Moderator',
      isCreator: true,
    })
  }
  return team
}

const moderatorCount = (forum) => teamOf(forum).filter(member => member.role !== 'Admin').length
const adminCount = (forum) => teamOf(forum).filter(member => member.role === 'Admin').length

const formatDate = (value) => {
  if (!value) return ''
  return new Date(value).toLocaleDateString('zh-CN')
}

const loadForumPosts = async (forum) => {
  const state = postDraft(forum)
  try {
    state.loading = true
    const res = await getPosts({
      forumId: forum.forumID,
      pageSize: 30,
      ...(state.tab ? { status: state.tab } : {}),
    })
    state.posts = Array.isArray(res.data) ? res.data : []
    state.loaded = true
  } catch (e) {
    state.posts = []
    error.value = '无法加载版块帖子: ' + (e.response?.data?.message || e.message)
  } finally {
    state.loading = false
  }
}

const switchPostTab = (forum, tab) => {
  const state = postDraft(forum)
  if (state.tab === tab) return
  state.tab = tab
  loadForumPosts(forum)
}

// 根据帖子当前状态给出可用的管理操作（置顶/加精/封禁/审核等）
const postActions = (post) => {
  switch (post.status) {
    case 'Pinned':
      return [
        { label: '取消置顶', value: 'unpin', primary: true },
        { label: '封禁', value: 'ban', danger: true },
        { label: '删除', value: 'delete', danger: true },
      ]
    case 'Elite':
      return [
        { label: '取消加精', value: 'unelite', primary: true },
        { label: '封禁', value: 'ban', danger: true },
        { label: '删除', value: 'delete', danger: true },
      ]
    case 'PendingReview':
      return [
        { label: '通过', value: 'approve', primary: true },
        { label: '拒绝', value: 'reject', danger: true },
      ]
    case 'Banned':
      return [
        { label: '恢复', value: 'restore', primary: true },
        { label: '删除', value: 'delete', danger: true },
      ]
    default:
      return [
        { label: '置顶', value: 'pin', primary: true },
        { label: '加精', value: 'elite' },
        { label: '封禁', value: 'ban', danger: true },
        { label: '删除', value: 'delete', danger: true },
      ]
  }
}

const handlePostAction = async (forum, post, action) => {
  const state = postDraft(forum)
  try {
    state.actingId = post.postID
    error.value = null
    const res = await changePostStatus(post.postID, { action })
    notice.value = res.data?.message || '帖子状态已更新。'
    await loadForumPosts(forum)
    await loadForums()
  } catch (e) {
    error.value = '帖子操作失败: ' + (e.response?.data?.message || e.message)
  } finally {
    state.actingId = null
  }
}

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

const handleAvatarPick = async (forum, event) => {
  const input = event.target
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  try {
    uploadingAvatarId.value = forum.forumID
    const res = await uploadForumAvatar(forum.forumID, file)
    forum.avatarUrl = res.data?.avatarUrl || ''
    notice.value = '版块头像已更新。'
    await loadForums()
  } catch (e) {
    error.value = '上传版块头像失败: ' + (e.response?.data?.message || e.message)
  } finally {
    uploadingAvatarId.value = null
  }
}

const handleRemoveAvatar = async (forum) => {
  try {
    uploadingAvatarId.value = forum.forumID
    await removeForumAvatar(forum.forumID)
    forum.avatarUrl = ''
    notice.value = '版块头像已移除。'
    await loadForums()
  } catch (e) {
    error.value = '移除版块头像失败: ' + (e.response?.data?.message || e.message)
  } finally {
    uploadingAvatarId.value = null
  }
}

const handleSearchUsers = async (forum) => {
  const state = draft(forum)
  const keyword = state.keyword.trim()
  if (!keyword) return

  try {
    state.searching = true
    error.value = null
    const res = await searchUsers(keyword)
    const memberIds = new Set(teamOf(forum).map(member => member.userID))
    state.results = (Array.isArray(res.data) ? res.data : [])
      .filter(user => !memberIds.has(user.userID))
    state.searched = true
  } catch (e) {
    error.value = '搜索用户失败: ' + (e.response?.data?.message || e.message)
  } finally {
    state.searching = false
  }
}

const handleAssignManager = async (forum, user) => {
  const state = draft(forum)
  try {
    state.assigningId = user.userID
    error.value = null
    const res = await assignForumManager(forum.forumID, user.userID, state.assignRole)
    forum.managers = [
      ...(forum.managers || []),
      { userID: user.userID, username: user.username, email: user.email, role: state.assignRole },
    ]
    state.results = state.results.filter(item => item.userID !== user.userID)
    notice.value = res.data?.message || '管理人员已指派。'
    await loadForums()
  } catch (e) {
    error.value = '指派管理人员失败: ' + (e.response?.data?.message || e.message)
  } finally {
    state.assigningId = null
  }
}

const handleRemoveManager = async (forum, manager) => {
  const name = manager.username || manager.email
  const roleName = manager.role === 'Admin' ? '管理员' : '版主'
  if (!window.confirm(`确定要移除${roleName}「${name}」吗？移除后对方将无法管理本版块。`)) return

  const state = draft(forum)
  try {
    state.removingId = manager.userID
    error.value = null
    const res = await removeForumManager(forum.forumID, manager.userID)
    forum.managers = (forum.managers || []).filter(item => item.userID !== manager.userID)
    notice.value = res.data?.message || '管理人员已移除。'
    await loadForums()
  } catch (e) {
    error.value = '移除管理人员失败: ' + (e.response?.data?.message || e.message)
  } finally {
    state.removingId = null
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
  width: min(960px, 100%);
  margin: 0 auto;
  color: #11172a;
}

/* ---------- 页头 ---------- */

.mf-page-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1.25rem;
}

.mf-page-title {
  margin: 0;
  font-size: 1.35rem;
  letter-spacing: -.02em;
}

.mf-page-sub {
  margin-top: .3rem;
  color: #687286;
  font-size: .82rem;
}

.mf-create-btn {
  flex: 0 0 auto;
  padding: .6rem 1.1rem;
  border: 0;
  border-radius: 9px;
  background: #2f7ee0;
  color: #fff;
  cursor: pointer;
  font: inherit;
  font-size: .84rem;
  font-weight: 700;
  box-shadow: 0 6px 14px rgba(47, 126, 224, .22);
  transition: background .15s;
}

.mf-create-btn:hover {
  background: #266bc4;
}

/* ---------- 按钮体系 ---------- */

.mf-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 34px;
  padding: .42rem .9rem;
  border: 1px solid #d8dde6;
  border-radius: 8px;
  background: #fff;
  color: #3c4659;
  cursor: pointer;
  font: inherit;
  font-size: .78rem;
  font-weight: 650;
  transition: background .15s, border-color .15s, color .15s;
}

.mf-btn:hover:not(:disabled) {
  background: #f2f7fd;
  border-color: #b9d4f1;
}

.mf-btn:disabled {
  opacity: .55;
  cursor: default;
}

.mf-btn-primary {
  border-color: transparent;
  background: #2f7ee0;
  color: #fff;
}

.mf-btn-primary:hover:not(:disabled) {
  background: #266bc4;
  border-color: transparent;
}

.mf-btn-ghost {
  background: transparent;
}

.mf-btn-danger {
  border-color: transparent;
  background: transparent;
  color: #98a1b3;
}

.mf-btn-danger:hover:not(:disabled) {
  background: #fdecec;
  border-color: transparent;
  color: #d64545;
}

.mf-btn-sm {
  min-height: 30px;
  padding: .3rem .75rem;
  font-size: .74rem;
}

.mf-btn:focus-visible,
.mf-create-btn:focus-visible,
.mf-panel :is(input, textarea, select):focus-visible {
  outline: 2px solid #2f7ee0;
  outline-offset: 2px;
}

/* ---------- 卡片列表 ---------- */

.mf-list {
  display: grid;
  gap: 1.25rem;
}

.mf-card {
  border: 1px solid #e4e7ec;
  border-radius: 16px;
  background: #fff;
  overflow: hidden;
}

/* ---------- 卡片头部 ---------- */

.mf-head {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1.25rem 1.4rem;
  border-bottom: 1px solid #eef1f5;
  background: linear-gradient(180deg, #f9fbfe, #fff);
}

.mf-avatar {
  width: 60px;
  height: 60px;
  flex: 0 0 auto;
  border-radius: 14px;
  object-fit: cover;
  background: #eef1f6;
}

.mf-avatar-fallback {
  display: grid;
  place-items: center;
  color: #fff;
  background: linear-gradient(135deg, #4d9bf0, #2f7ee0);
  font-size: 1.4rem;
  font-weight: 800;
}

.mf-head-main {
  flex: 1;
  min-width: 0;
}

.mf-name-row {
  display: flex;
  align-items: center;
  gap: .6rem;
  flex-wrap: wrap;
}

.mf-name {
  margin: 0;
  font-size: 1.1rem;
  letter-spacing: -.01em;
}

.mf-pill {
  display: inline-flex;
  align-items: center;
  gap: .35rem;
  padding: .2rem .65rem;
  border-radius: 999px;
  font-size: .68rem;
  font-weight: 750;
}

.mf-pill-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: currentColor;
}

.mf-pill-on {
  background: #e5f6ec;
  color: #1d8a4e;
}

.mf-pill-off {
  background: #fdf3d7;
  color: #a06a08;
}

.mf-desc {
  margin: .35rem 0 0;
  color: #5b6577;
  font-size: .8rem;
  overflow: hidden;
  text-overflow: ellipsis;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
}

.mf-id {
  display: block;
  margin-top: .3rem;
  color: #9aa3b3;
  font-size: .68rem;
  font-weight: 700;
  letter-spacing: .06em;
}

.mf-stats {
  display: flex;
  gap: .5rem;
  margin: 0;
}

.mf-stat {
  min-width: 64px;
  padding: .5rem .65rem;
  border: 1px solid #eef1f5;
  border-radius: 10px;
  background: #fbfcfe;
  text-align: center;
}

.mf-stat dt {
  color: #98a1b3;
  font-size: .64rem;
  font-weight: 700;
  letter-spacing: .08em;
}

.mf-stat dd {
  margin: .1rem 0 0;
  color: #11172a;
  font-size: 1.05rem;
  font-weight: 750;
}

/* ---------- 双栏面板 ---------- */

.mf-grid {
  display: grid;
  grid-template-columns: 1.08fr 1fr;
  gap: 0;
}

.mf-panel {
  display: flex;
  flex-direction: column;
  gap: .9rem;
  padding: 1.25rem 1.4rem;
  min-width: 0;
}

.mf-panel + .mf-panel {
  border-left: 1px solid #eef1f5;
}

.mf-panel-title {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: .5rem;
  margin: 0;
  padding-bottom: .65rem;
  border-bottom: 1px solid #eef1f5;
  color: #11172a;
  font-size: .9rem;
}

.mf-panel-count {
  color: #98a1b3;
  font-size: .7rem;
  font-weight: 650;
}

.mf-panel-foot {
  display: flex;
  justify-content: flex-end;
  margin-top: auto;
  padding-top: .35rem;
}

/* ---------- 表单 ---------- */

.mf-field-row {
  display: grid;
  grid-template-columns: 1.6fr 1fr;
  gap: .85rem;
}

.mf-field {
  display: grid;
  gap: .4rem;
  min-width: 0;
}

.mf-label {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  color: #45536b;
  font-size: .76rem;
  font-weight: 700;
}

.mf-counter {
  color: #b0b8c6;
  font-size: .66rem;
  font-weight: 600;
}

.mf-panel :is(input, textarea, select) {
  width: 100%;
  padding: .62rem .75rem;
  border: 1px solid #dfe2e8;
  border-radius: 9px;
  background: #fff;
  color: #11172a;
  font: inherit;
  font-size: .84rem;
  transition: border-color .15s, box-shadow .15s;
}

.mf-panel :is(input, textarea, select):focus {
  border-color: #2f7ee0;
  box-shadow: 0 0 0 3px rgba(47, 126, 224, .12);
  outline: none;
}

.mf-panel textarea {
  resize: vertical;
}

.mf-avatar-row {
  display: grid;
  gap: .4rem;
  padding: .85rem;
  border: 1px dashed #dfe2e8;
  border-radius: 10px;
  background: #fbfcfe;
}

.mf-avatar-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: .5rem;
}

.mf-hint {
  width: 100%;
  color: #98a1b3;
  font-size: .68rem;
}

.visually-hidden-input {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
  white-space: nowrap;
}

/* ---------- 管理团队 ---------- */

.mf-manager-list {
  display: grid;
  gap: .5rem;
  margin: 0;
  padding: 0;
  list-style: none;
}

.mf-manager-item {
  display: flex;
  align-items: center;
  gap: .7rem;
  padding: .55rem .7rem;
  border: 1px solid #eef1f5;
  border-radius: 10px;
  background: #fbfcfe;
}

.mf-manager-avatar {
  display: grid;
  place-items: center;
  width: 32px;
  height: 32px;
  flex: 0 0 auto;
  border-radius: 50%;
  color: #fff;
  background: linear-gradient(135deg, #4d9bf0, #2f7ee0);
  font-size: .82rem;
  font-weight: 750;
}

.mf-manager-info {
  display: grid;
  flex: 1;
  min-width: 0;
}

.mf-manager-info b {
  color: #11172a;
  font-size: .82rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.mf-manager-info small {
  color: #98a1b3;
  font-size: .68rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.mf-empty-note {
  margin: 0;
  padding: .85rem;
  border: 1px dashed #dfe2e8;
  border-radius: 10px;
  color: #98a1b3;
  font-size: .76rem;
  text-align: center;
}

/* ---------- 搜索指派 ---------- */

.mf-assign {
  display: grid;
  gap: .5rem;
  margin-top: auto;
  padding-top: .9rem;
  border-top: 1px solid #eef1f5;
}

.mf-search-row {
  display: flex;
  gap: .5rem;
}

.mf-search-row input {
  flex: 1;
  min-width: 0;
  padding: .62rem .75rem;
  border: 1px solid #dfe2e8;
  border-radius: 9px;
  font: inherit;
  font-size: .82rem;
  transition: border-color .15s, box-shadow .15s;
}

.mf-search-row input:focus {
  border-color: #2f7ee0;
  box-shadow: 0 0 0 3px rgba(47, 126, 224, .12);
  outline: none;
}

.mf-results {
  display: grid;
  gap: .4rem;
  max-height: 236px;
  overflow-y: auto;
}

.mf-result-item {
  display: flex;
  align-items: center;
  gap: .7rem;
  padding: .5rem .7rem;
  border: 1px solid #eef1f5;
  border-radius: 10px;
  background: #fff;
}

/* ---------- 空状态 ---------- */

.mf-empty {
  border: 1px dashed #d9dbe5;
  border-radius: 16px;
  background: #fafbff;
  gap: .75rem;
}

.mf-empty p {
  color: #687286;
}

/* ---------- 面板折叠 ---------- */

.mf-toggle {
  flex: 0 0 auto;
  align-self: center;
  padding: .5rem .85rem;
  border: 1px solid #d8dde6;
  border-radius: 9px;
  background: #fff;
  color: #2f7ee0;
  cursor: pointer;
  font: inherit;
  font-size: .76rem;
  font-weight: 700;
  transition: background .15s, border-color .15s;
}

.mf-toggle:hover {
  background: #f2f7fd;
  border-color: #b9d4f1;
}

.mf-toggle[aria-expanded='true'] {
  background: #e6f1fc;
  border-color: #b9d4f1;
}

/* ---------- 角色徽章 ---------- */

.mf-role-badge {
  display: inline-block;
  margin-left: .3rem;
  padding: .08rem .4rem;
  border-radius: 999px;
  font-size: .62rem;
  font-weight: 750;
  vertical-align: middle;
}

.mf-role-moderator {
  background: #e6f1fc;
  color: #266bc4;
}

.mf-role-admin {
  background: #eef0f4;
  color: #5b6577;
}

.mf-role-creator {
  background: #fdf3d7;
  color: #a06a08;
}

.mf-role-select {
  flex: 0 0 auto;
  width: auto !important;
  padding: .5rem .6rem;
}

.mf-assign-hint {
  margin: auto 0 0;
  padding: .85rem;
  border: 1px dashed #dfe2e8;
  border-radius: 10px;
  color: #98a1b3;
  font-size: .74rem;
  text-align: center;
}

/* ---------- 帖子管理 ---------- */

.mf-posts-panel {
  border-top: 1px solid #eef1f5;
}

.mf-post-tabs {
  display: flex;
  gap: .5rem;
}

.mf-post-tabs button {
  padding: .38rem .85rem;
  border: 1px solid #d8dde6;
  border-radius: 999px;
  background: #fff;
  color: #5f6b7c;
  cursor: pointer;
  font: inherit;
  font-size: .74rem;
  font-weight: 650;
  transition: background .15s, border-color .15s, color .15s;
}

.mf-post-tabs button:hover {
  border-color: #b9d4f1;
  color: #2f7ee0;
}

.mf-post-tabs button.active {
  border-color: transparent;
  background: #2f7ee0;
  color: #fff;
}

.mf-post-list {
  display: grid;
  gap: .5rem;
  margin: 0;
  padding: 0;
  list-style: none;
  max-height: 340px;
  overflow-y: auto;
}

.mf-post-item {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: .5rem .7rem;
  padding: .55rem .7rem;
  border: 1px solid #eef1f5;
  border-radius: 10px;
  background: #fbfcfe;
}

.mf-post-tag {
  flex: 0 0 auto;
  padding: .1rem .38rem;
  border-radius: 4px;
  font-size: .62rem;
  font-weight: 750;
}

.mf-post-tag-pinned {
  background: #e6f1fc;
  color: #2f7ee0;
}

.mf-post-tag-elite {
  background: #f25d5d;
  color: #fff;
}

.mf-post-tag-review {
  background: #fdf3d7;
  color: #a06a08;
}

.mf-post-tag-banned {
  background: #fdecec;
  color: #d64545;
}

.mf-post-title {
  flex: 1;
  min-width: 8rem;
  color: #11172a;
  font-size: .82rem;
  font-weight: 650;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.mf-post-meta {
  flex: 0 0 auto;
  color: #98a1b3;
  font-size: .68rem;
}

.mf-post-actions {
  display: flex;
  flex-wrap: wrap;
  gap: .35rem;
  margin-left: auto;
}

.mf-post-danger {
  border-color: transparent;
  background: transparent;
  color: #98a1b3;
}

.mf-post-danger:hover:not(:disabled) {
  background: #fdecec;
  color: #d64545;
}

/* ---------- 响应式 ---------- */

@media (max-width: 860px) {
  .mf-grid {
    grid-template-columns: 1fr;
  }

  .mf-panel + .mf-panel {
    border-left: 0;
    border-top: 1px solid #eef1f5;
  }

  .mf-head {
    flex-wrap: wrap;
  }

  .mf-stats {
    width: 100%;
  }

  .mf-stat {
    flex: 1;
  }

  .mf-toggle {
    width: 100%;
  }
}

@media (max-width: 560px) {
  .mf-field-row {
    grid-template-columns: 1fr;
  }

  .mf-page-head {
    flex-direction: column;
  }

  .mf-create-btn {
    align-self: flex-start;
  }
}
</style>
