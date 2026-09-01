<template>
  <div class="page-container system-page">
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

    <SectionMessage :message="sectionMessages.system" />
    <section class="admin-launch-section">
      <div class="admin-launch-heading">
        <div><span>管理工具</span><h2>选择要管理的内容</h2></div>
        <p>各模块独立打开，减少页面信息干扰。</p>
      </div>
      <div class="admin-launch-grid">
        <button type="button" @click="activeAdminPanel = 'forums'"><span class="launch-icon">版</span><span><strong>论坛版块</strong><small>{{ forums.length }} 个版块</small></span><b>→</b></button>
        <button type="button" @click="activeAdminPanel = 'users'"><span class="launch-icon">用</span><span><strong>用户管理</strong><small>{{ users.length }} 位用户</small></span><b>→</b></button>
        <button type="button" @click="activeAdminPanel = 'audits'"><span class="launch-icon">审</span><span><strong>内容审核</strong><small>{{ postAudits.length }} 条待处理</small></span><b>→</b></button>
        <button type="button" @click="activeAdminPanel = 'posts'"><span class="launch-icon">帖</span><span><strong>帖子管理</strong><small>状态与可见性</small></span><b>→</b></button>
        <button type="button" @click="activeAdminPanel = 'disputes'"><span class="launch-icon">纠</span><span><strong>交易纠纷</strong><small>{{ disputes.length }} 个工单</small></span><b>→</b></button>
        <button type="button" @click="activeAdminPanel = 'reports'"><span class="launch-icon">举</span><span><strong>举报工单</strong><small>{{ reports.length }} 条举报</small></span><b>→</b></button>
        <button type="button" @click="openAnnouncementPanel"><span class="launch-icon">公</span><span><strong>系统公告</strong><small>{{ announcementCount }} 条公告</small></span><b>→</b></button>
      </div>
    </section>

    <ModalDialog
      :visible="!!activeAdminPanel"
      variant="panel"
      kicker="管理工具"
      :title="adminPanelTitles[activeAdminPanel] || ''"
      @close="activeAdminPanel = null"
    >

    <template v-if="activeAdminPanel === 'forums'">
    <div class="admin-section-heading"><span>社区配置</span><h2>论坛版块</h2><p>维护版块信息与版主管理关系。</p></div>
    <div class="card">
      <h2>论坛版块管理</h2>
      <SectionMessage :message="sectionMessages.forums" />
      <form class="inline-form" @submit.prevent="handleCreateForum">
        <input v-model="forumForm.forumName" type="text" placeholder="版块名称" required />
        <input v-model="forumForm.description" type="text" placeholder="版块描述" />
        <button class="btn btn-primary" type="submit">创建版块</button>
      </form>

      <div class="forum-admin-list">
        <article v-for="forum in forums" :key="forum.forumID" class="forum-admin-item">
          <div>
            <strong>{{ forum.forumName }}</strong>
            <p>{{ forum.description || '暂无描述' }}</p>
            <small>帖子 {{ forum.postCount || 0 }} · {{ forum.status }}</small>
            <div class="manager-tags">
              <span v-for="manager in forum.managers || []" :key="manager.userID" class="manager-tag">
                {{ manager.username || manager.email }}
                <button type="button" @click="handleRemoveManager(forum, manager)">×</button>
              </span>
              <span v-if="!forum.managers?.length" class="muted inline-muted">暂无版主</span>
            </div>
          </div>
          <form class="manager-form" @submit.prevent="handleAssignManager(forum)">
            <select v-model.number="forumManagerDrafts[forum.forumID]" required>
              <option disabled value="">选择版主</option>
              <option v-for="user in users" :key="user.userID" :value="user.userID">
                {{ user.username }} · {{ user.email }}
              </option>
            </select>
            <button class="btn" type="submit">指派</button>
          </form>
        </article>
        <div v-if="forums.length === 0" class="muted">暂无论坛版块</div>
      </div>
    </div>
    </template>

    <template v-if="activeAdminPanel === 'users'">
    <div class="admin-section-heading"><span>用户治理</span><h2>用户管理</h2><p>创建用户并调整信用。</p></div>
    <div class="card">
      <h2>用户管理</h2>
      <SectionMessage :message="sectionMessages.users" />
      <form class="user-create-form" @submit.prevent="handleCreateUser">
        <input v-model="userForm.username" type="text" placeholder="用户名" required />
        <input v-model="userForm.email" type="email" placeholder="校园邮箱" required />
        <input v-model="userForm.password" type="password" placeholder="初始密码" required />
        <button class="btn btn-primary" type="submit">创建用户</button>
      </form>
      <div v-if="loadingUsers" class="loading">加载中...</div>
      <div v-else class="table-wrapper">
        <table>
          <thead>
            <tr>
              <th>ID</th>
              <th>用户名</th>
              <th>邮箱</th>
              <th>信用分</th>
              <th>状态</th>
              <th>角色</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="user in users" :key="user.userID">
              <td>{{ user.userID }}</td>
              <td>{{ user.username }}</td>
              <td>{{ user.email }}</td>
              <td>
                <div class="credit-cell">
                  <strong>{{ user.credit }}</strong>
                  <div class="credit-actions">
                    <button class="btn credit-mini-button" type="button" @click="openCreditDialog(user)">
                      调整
                    </button>
                    <button class="btn credit-mini-button" type="button" @click="openCreditHistoryDialog(user)">
                      记录
                    </button>
                  </div>
                </div>
              </td>
              <td>
                <span :class="['badge', user.status === 'Active' ? 'badge-green' : 'badge-yellow']">
                  {{ user.status || '未知' }}
                </span>
              </td>
              <td>
                <span v-for="role in user.roles || []" :key="role" class="badge badge-green">{{ role }}</span>
                <span v-if="!user.roles?.length" class="muted">暂无角色</span>
              </td>
            </tr>
            <tr v-if="users.length === 0">
              <td colspan="7" class="empty-cell">暂无数据</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
    </template>

    <template v-if="activeAdminPanel === 'audits'">
    <div class="admin-section-heading"><span>内容与交易治理</span><h2>待处理事项</h2><p>审核社区内容，处理帖子状态、交易纠纷与举报。</p></div>
    <div class="card">
      <h2>内容审核队列</h2>
      <SectionMessage :message="sectionMessages.audits" />
      <div v-if="postAudits.length === 0" class="muted">暂无待审核内容</div>
      <div v-else class="report-list">
        <article v-for="audit in postAudits" :key="audit.auditID" class="report-item">
          <div>
            <strong>{{ auditTitle(audit) }}</strong>
            <p>{{ auditContent(audit) }}</p>
            <small>{{ audit.targetType || 'Post' }} #{{ audit.targetID }} · 命中词：{{ audit.triggerWord || '-' }} · {{ audit.status }}</small>
          </div>
          <div class="report-actions">
            <button class="btn" @click="handlePostAudit(audit, 'approve')">通过</button>
            <button class="btn" @click="handlePostAudit(audit, 'reject')">拒绝</button>
          </div>
        </article>
      </div>
    </div>
    </template>

    <template v-if="activeAdminPanel === 'posts'">
    <div class="card">
      <h2>帖子状态管理</h2>
      <SectionMessage :message="sectionMessages.posts" />
      <div class="toolbar-row">
        <select v-model="postStatusFilter" @change="loadManagedPosts">
          <option value="">可见帖子</option>
          <option value="Active">正常</option>
          <option value="Pinned">置顶</option>
          <option value="Elite">精华</option>
          <option value="PendingReview">待审核</option>
          <option value="Banned">已封禁</option>
          <option value="Deleted">已删除</option>
        </select>
        <button class="btn" @click="loadManagedPosts">刷新</button>
      </div>
      <div v-if="managedPosts.length === 0" class="muted">暂无帖子</div>
      <div v-else class="report-list">
        <article v-for="post in managedPosts" :key="post.postID" class="report-item">
          <div>
            <strong>{{ post.title }}</strong>
            <p>{{ post.contentPreview || '暂无内容' }}</p>
            <small>{{ post.forumName || '未分区' }} · {{ post.username || '匿名用户' }} · {{ post.status }}</small>
          </div>
          <div class="report-actions post-status-actions">
            <button class="btn" @click="handlePostStatus(post, 'pin')">置顶</button>
            <button class="btn" @click="handlePostStatus(post, 'elite')">精华</button>
            <button class="btn" @click="handlePostStatus(post, 'ban')">封禁</button>
            <button class="btn" @click="handlePostStatus(post, 'restore')">恢复</button>
            <button class="btn" @click="handlePostStatus(post, 'delete')">删除</button>
          </div>
        </article>
      </div>
    </div>
    </template>

    <template v-if="activeAdminPanel === 'disputes'">
    <div class="card">
      <h2>交易纠纷仲裁</h2>
      <SectionMessage :message="sectionMessages.disputes" />
      <div v-if="disputes.length === 0" class="muted">暂无纠纷工单</div>
      <div v-else class="report-list">
        <article v-for="dispute in disputes" :key="dispute.ticketID" class="report-item dispute-item">
          <div>
            <strong>订单 #{{ dispute.transactionID }} · 工单 #{{ dispute.ticketID }}</strong>
            <div class="dispute-context">
              <span>{{ dispute.productTitle || '未知商品' }}</span>
              <span>金额 ¥{{ Number(dispute.transactionAmount || 0).toFixed(2) }}</span>
              <span>买家 {{ dispute.buyerName || '-' }}</span>
              <span>卖家 {{ dispute.sellerName || '-' }}</span>
            </div>
            <p>{{ dispute.reason }}</p>
            <small>
              {{ dispute.username || '申请人' }} · {{ dispute.status }}
              <template v-if="dispute.arbitratorName"> · 仲裁员 {{ dispute.arbitratorName }}</template>
            </small>
            <div v-if="dispute.decision" class="dispute-result">
              <strong>仲裁结果</strong>
              <p>{{ dispute.decision }}</p>
              <small>
                退款 ¥{{ Number(dispute.refundAmount || 0).toFixed(2) }}
                <template v-if="dispute.resolvedTime"> · {{ formatDate(dispute.resolvedTime) }}</template>
              </small>
            </div>
          </div>
          <form class="dispute-form" @submit.prevent="handleResolveDispute(dispute)">
            <input
              v-model="disputeDrafts[dispute.ticketID].decision"
              type="text"
              placeholder="仲裁结论"
              :disabled="dispute.status !== 'Open'"
              required
            />
            <input
              v-model.number="disputeDrafts[dispute.ticketID].refundAmount"
              type="number"
              min="0"
              :max="dispute.transactionAmount || undefined"
              step="0.01"
              placeholder="退款金额"
              :disabled="dispute.status !== 'Open'"
              required
            />
            <button class="btn" type="submit" :disabled="dispute.status !== 'Open'">结案</button>
          </form>
        </article>
      </div>
    </div>
    </template>

    <template v-if="activeAdminPanel === 'reports'">
    <div class="card">
      <h2>举报工单</h2>
      <SectionMessage :message="sectionMessages.reports" />
      <div v-if="reports.length === 0" class="muted">暂无举报</div>
      <div v-else class="report-list">
        <article v-for="report in reports" :key="report.reportID" class="report-item">
          <div>
            <strong>{{ report.targetType }} #{{ report.targetID }}</strong>
            <p>{{ report.reason }}</p>
            <small>{{ report.reporterName || '匿名用户' }} · {{ report.status }}</small>
          </div>
          <div class="report-actions">
            <button class="btn" :disabled="report.status !== 'Pending'" @click="handleReviewReport(report, 'approve')">
              通过
            </button>
            <button class="btn" :disabled="report.status !== 'Pending'" @click="handleReviewReport(report, 'reject')">
              驳回
            </button>
          </div>
        </article>
      </div>
    </div>
    </template>

    <template v-if="activeAdminPanel === 'announcements'">
    <div class="admin-section-heading"><span>运营工具</span><h2>系统公告</h2><p>发布系统公告，向全体用户或指定用户推送通知。</p></div>
    <div class="card">
      <h2>发布系统公告</h2>
      <SectionMessage :message="sectionMessages.announcements" />
      <form class="announcement-form" @submit.prevent="handleCreateAnnouncement">
        <input v-model="announcementForm.title" type="text" placeholder="系统公告标题" required />
        <textarea v-model="announcementForm.content" placeholder="系统公告内容" rows="3" required></textarea>
        <div class="announcement-form-actions">
          <label class="announcement-target-label">
            <span>推送范围</span>
            <select v-model="announcementForm.targetType">
              <option value="all">全体用户</option>
              <option value="specified">指定用户</option>
            </select>
          </label>
          <div v-if="announcementForm.targetType === 'specified'" class="announcement-user-select">
            <div class="announcement-user-chips" v-if="announcementForm.receiverUserIDs.length">
              <span v-for="uid in announcementForm.receiverUserIDs" :key="uid" class="manager-tag">
                {{ getUserDisplayName(uid) }}
                <button type="button" @click="removeReceiverUser(uid)">×</button>
              </span>
            </div>
            <div class="announcement-user-picker">
              <select v-model.number="announcementUserDraft" @change="addReceiverUser">
                <option disabled :value="0">选择用户添加</option>
                <option v-for="user in users" :key="user.userID" :value="user.userID"
                  :disabled="announcementForm.receiverUserIDs.includes(user.userID)">
                  {{ user.username }} · {{ user.email }}
                </option>
              </select>
            </div>
          </div>
          <button class="btn btn-primary" type="submit" :disabled="loadingAnnouncements">发布</button>
        </div>
      </form>
    </div>
    <div class="card">
      <h2>系统公告列表</h2>
      <div class="toolbar-row">
        <button class="btn" @click="loadAnnouncements">刷新</button>
      </div>
      <div v-if="loadingAnnouncements" class="loading">加载中...</div>
      <div v-else-if="announcements.length === 0" class="muted">暂无系统公告</div>
      <div v-else class="report-list">
        <article v-for="announcement in announcements" :key="announcement.id" class="report-item">
          <div>
            <strong>{{ announcement.title }}</strong>
            <p>{{ announcement.content }}</p>
            <small>{{ formatDate(announcement.date) }}</small>
          </div>
          <div class="report-actions">
            <button class="btn" @click="handleDeleteAnnouncement(announcement.id)">删除</button>
          </div>
        </article>
      </div>
    </div>
    </template>

    </ModalDialog>

    <ModalDialog
      :visible="!!selectedCreditUser"
      variant="dialog"
      title="调整信用分"
      :subtitle="selectedCreditUser ? `${selectedCreditUser.username} · 当前信用分 ${selectedCreditUser.credit}` : ''"
      tag="form"
      @close="closeCreditDialog"
      @submit.prevent="handleAdjustCredit(selectedCreditUser.userID)"
    >

        <label class="credit-form-label">
          调整分值
          <input
            v-model.number="creditDrafts[selectedCreditUser.userID].credit"
            type="number"
            min="-1000"
            max="1000"
            placeholder="正数加分，负数扣分"
            required
          />
        </label>

        <label class="credit-form-label">
          调整原因
          <textarea
            class="credit-form-textarea"
            v-model="creditDrafts[selectedCreditUser.userID].reason"
            maxlength="500"
            placeholder="请填写本次调整原因"
            required
          />
        </label>

      <template #actions>
        <button class="btn" type="button" @click="closeCreditDialog">取消</button>
        <button class="btn btn-primary" type="submit">确认调整</button>
      </template>
    </ModalDialog>

    <ModalDialog
      :visible="!!selectedCreditHistoryUser"
      variant="dialog"
      title="信用分调整记录"
      :subtitle="selectedCreditHistoryUser ? `${selectedCreditHistoryUser.username} · 当前信用分 ${selectedCreditHistoryUser.credit}` : ''"
      dialog-class="credit-history-dialog"
      @close="closeCreditHistoryDialog"
    >

        <div v-if="loadingCreditHistory" class="loading">加载中...</div>
        <div v-else-if="creditHistoryRecords.length === 0" class="muted">暂无信用分调整记录</div>
        <div v-else class="credit-history-list">
          <article
            v-for="record in creditHistoryRecords"
            :key="record.creditAdjustmentId"
            class="credit-history-item"
          >
            <div>
              <strong>{{ record.description || '信用分调整' }}</strong>
              <small>
                {{ formatDate(record.adjustTime) }}
                <template v-if="record.operatorName"> · 操作人：{{ record.operatorName }}</template>
              </small>
              <small v-if="record.beforeCredit != null && record.afterCredit != null">
                {{ record.beforeCredit }} -> {{ record.afterCredit }}
              </small>
            </div>
            <b :class="record.changePoints >= 0 ? 'credit-up' : 'credit-down'">
              {{ record.changePoints >= 0 ? '+' : '' }}{{ record.changePoints }}
            </b>
          </article>
        </div>
    </ModalDialog>
  </div>
</template>

<script setup>
import { ref, onMounted, h } from 'vue'
import ModalDialog from '../components/ModalDialog.vue'
import {
  adjustCredit,
  approvePostAudit,
  assignForumManager,
  changePostStatus,
  createForum,
  createUser,
  getForums,
  getHealth,
  getPostAudits,
  getPosts,
  getProducts,
  getReports,
  getAnnouncements,
  createSystemNotification,
  deleteNotification,
  getDisputes,
  getUserCreditAdjustments,
  getUsers,
  rejectPostAudit,
  removeForumManager,
  resolveDispute,
  reviewReport,
} from '../api'

const stats = ref({ users: '-', forums: '-', posts: '-', products: '-' })
const health = ref(null)
const users = ref([])
const forums = ref([])
const reports = ref([])
const postAudits = ref([])
const managedPosts = ref([])
const disputes = ref([])
const disputeDrafts = ref({})
const forumManagerDrafts = ref({})
const creditDrafts = ref({})
const selectedCreditUser = ref(null)
const selectedCreditHistoryUser = ref(null)
const creditHistoryRecords = ref([])
const loadingCreditHistory = ref(false)
const announcementForm = ref({
  title: '',
  content: '',
  targetType: 'all',
  receiverUserIDs: [],
})
const announcementUserDraft = ref(0)
const announcements = ref([])
const announcementCount = ref(0)
const loadingAnnouncements = ref(false)

const postStatusFilter = ref('')
const activeAdminPanel = ref(null)
const adminPanelTitles = {
  forums: '论坛版块',
  users: '用户管理',
  audits: '内容审核',
  posts: '帖子管理',
  disputes: '交易纠纷',
  reports: '举报工单',
  announcements: '系统公告',
}
const loading = ref(true)
const loadingUsers = ref(true)
const messageTimers = new Map()
const sectionMessages = ref({
  system: null,
  forums: null,
  users: null,
  audits: null,
  posts: null,
  disputes: null,
  reports: null,
  announcements: null,
})

const SectionMessage = (props) => {
  if (!props.message) return null
  return h('div', {
    class: [`${props.message.type}-message`, 'section-message'],
  }, props.message.text)
}

const forumForm = ref({
  forumName: '',
  description: '',
})

const userForm = ref({
  username: '',
  email: '',
  password: '',
})

const formatDate = (value) => {
  if (!value) return ''
  return new Date(value).toLocaleString('zh-CN', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

const auditTitle = (audit) => {
  if ((audit.targetType || '').toLowerCase() === 'comment') {
    return `评论 #${audit.targetID}`
  }
  return audit.post?.title || `帖子 #${audit.targetID}`
}

const auditContent = (audit) => {
  if ((audit.targetType || '').toLowerCase() === 'comment') {
    return audit.comment?.content || '暂无评论内容'
  }
  return audit.post?.contentPreview || '暂无帖子内容'
}

const clearMessage = (section) => {
  if (messageTimers.has(section)) {
    clearTimeout(messageTimers.get(section))
    messageTimers.delete(section)
  }
  sectionMessages.value = {
    ...sectionMessages.value,
    [section]: null,
  }
}

const showMessage = (section, type, text) => {
  clearMessage(section)
  sectionMessages.value = {
    ...sectionMessages.value,
    [section]: { type, text },
  }
  messageTimers.set(section, setTimeout(() => {
    clearMessage(section)
  }, 3000))
}

const loadStats = async () => {
  const [usersRes, forumsRes, posts, products, healthRes] = await Promise.allSettled([
    getUsers(),
    getForums(),
    getPosts(),
    getProducts(),
    getHealth(),
  ])

  stats.value = {
    users: usersRes.status === 'fulfilled' ? usersRes.value.data.length : 0,
    forums: forumsRes.status === 'fulfilled' ? forumsRes.value.data.length : 0,
    posts: posts.status === 'fulfilled' ? posts.value.data.length : 0,
    products: products.status === 'fulfilled' ? products.value.data.length : 0,
  }
  health.value = healthRes.status === 'fulfilled' ? healthRes.value.data : { status: 'unhealthy' }
  if (usersRes.status === 'fulfilled') {
    users.value = usersRes.value.data
    syncCreditDrafts()
  }
  if (forumsRes.status === 'fulfilled') forums.value = forumsRes.value.data
}

const syncCreditDrafts = () => {
  const next = {}
  for (const user of users.value) {
    next[user.userID] = creditDrafts.value[user.userID] || {
      credit: 0,
      reason: '',
    }
  }
  creditDrafts.value = next
}

const loadReports = async () => {
  const res = await getReports()
  reports.value = res.data
}

const loadPostAudits = async () => {
  const res = await getPostAudits()
  postAudits.value = res.data
}

const loadManagedPosts = async () => {
  const res = await getPosts({
    status: postStatusFilter.value || undefined,
    pageSize: 50,
  })
  managedPosts.value = res.data
}

const loadDisputes = async () => {
  const res = await getDisputes()
  disputes.value = res.data
  const next = {}
  for (const dispute of disputes.value) {
    next[dispute.ticketID] = disputeDrafts.value[dispute.ticketID] || {
      decision: '',
      refundAmount: 0,
    }
  }
  disputeDrafts.value = next
}

const loadForums = async () => {
  const res = await getForums()
  forums.value = res.data
}

const handleCreateForum = async () => {
  try {
    clearMessage('forums')
    await createForum(forumForm.value)
    forumForm.value = { forumName: '', description: '' }
    await loadForums()
    stats.value = { ...stats.value, forums: forums.value.length }
    showMessage('forums', 'success', '论坛版块已创建')
  } catch (e) {
    showMessage('forums', 'error', e.response?.data?.message || '论坛版块创建失败')
  }
}

const handleCreateUser = async () => {
  try {
    clearMessage('users')
    await createUser({
      username: userForm.value.username.trim(),
      email: userForm.value.email.trim(),
      password: userForm.value.password,
    })
    userForm.value = { username: '', email: '', password: '' }
    await loadStats()
    showMessage('users', 'success', '用户已创建')
  } catch (e) {
    showMessage('users', 'error', e.response?.data?.message || '用户创建失败')
  }
}

const handleAssignManager = async (forum) => {
  const userId = forumManagerDrafts.value[forum.forumID]
  if (!userId) return

  try {
    clearMessage('forums')
    await assignForumManager(forum.forumID, userId)
    forumManagerDrafts.value = { ...forumManagerDrafts.value, [forum.forumID]: '' }
    await loadForums()
    showMessage('forums', 'success', '版主已指派')
  } catch (e) {
    showMessage('forums', 'error', e.response?.data?.message || '版主指派失败')
  }
}

const handleRemoveManager = async (forum, manager) => {
  try {
    clearMessage('forums')
    await removeForumManager(forum.forumID, manager.userID)
    await loadForums()
    showMessage('forums', 'success', '版主已移除')
  } catch (e) {
    showMessage('forums', 'error', e.response?.data?.message || '版主移除失败')
  }
}

const openCreditDialog = (user) => {
  selectedCreditUser.value = user
  creditDrafts.value = {
    ...creditDrafts.value,
    [user.userID]: creditDrafts.value[user.userID] || { credit: 0, reason: '' },
  }
}

const closeCreditDialog = () => {
  selectedCreditUser.value = null
}

const openCreditHistoryDialog = async (user) => {
  selectedCreditHistoryUser.value = user
  creditHistoryRecords.value = []
  loadingCreditHistory.value = true

  try {
    clearMessage('users')
    const res = await getUserCreditAdjustments(user.userID)
    creditHistoryRecords.value = res.data
  } catch (e) {
    showMessage('users', 'error', e.response?.data?.message || '信用分记录加载失败')
    closeCreditHistoryDialog()
  } finally {
    loadingCreditHistory.value = false
  }
}

const closeCreditHistoryDialog = () => {
  selectedCreditHistoryUser.value = null
  creditHistoryRecords.value = []
  loadingCreditHistory.value = false
}

const handleAdjustCredit = async (userId) => {
  const draft = creditDrafts.value[userId]
  if (!draft) return

  try {
    clearMessage('users')
    await adjustCredit({
      userId,
      credit: Number(draft.credit),
      reason: draft.reason,
    })
    creditDrafts.value = {
      ...creditDrafts.value,
      [userId]: { credit: 0, reason: '' },
    }
    await loadStats()
    closeCreditDialog()
    showMessage('users', 'success', '信用分已调整')
  } catch (e) {
    showMessage('users', 'error', e.response?.data?.message || '信用分调整失败')
  }
}

const handleReviewReport = async (report, action) => {
  try {
    clearMessage('reports')
    await reviewReport(report.reportID, {
      action,
      result: action === 'approve' ? '举报成立，已处理目标内容' : '举报不成立',
    })
    await loadReports()
    showMessage('reports', 'success', '举报已处理')
  } catch (e) {
    showMessage('reports', 'error', e.response?.data?.message || '举报处理失败')
  }
}

const handlePostAudit = async (audit, action) => {
  try {
    clearMessage('audits')
    if (action === 'approve') await approvePostAudit(audit.auditID)
    else await rejectPostAudit(audit.auditID)
    await Promise.all([loadPostAudits(), loadManagedPosts(), loadStats()])
    showMessage('audits', 'success', '内容审核已处理')
  } catch (e) {
    showMessage('audits', 'error', e.response?.data?.message || '内容审核失败')
  }
}

const handlePostStatus = async (post, action) => {
  try {
    clearMessage('posts')
    await changePostStatus(post.postID, { action })
    await Promise.all([loadManagedPosts(), loadStats()])
    showMessage('posts', 'success', '帖子状态已更新')
  } catch (e) {
    showMessage('posts', 'error', e.response?.data?.message || '帖子状态更新失败')
  }
}

const getUserDisplayName = (uid) => {
  const user = users.value.find(u => u.userID === uid)
  return user ? `${user.username} (${user.email})` : `用户 #${uid}`
}

const addReceiverUser = () => {
  const uid = announcementUserDraft.value
  if (uid && !announcementForm.value.receiverUserIDs.includes(uid)) {
    announcementForm.value.receiverUserIDs.push(uid)
  }
  announcementUserDraft.value = 0
}

const removeReceiverUser = (uid) => {
  announcementForm.value.receiverUserIDs = announcementForm.value.receiverUserIDs.filter(id => id !== uid)
}

const openAnnouncementPanel = () => {
  activeAdminPanel.value = 'announcements'
  loadAnnouncements()
}

const loadAnnouncements = async () => {
  loadingAnnouncements.value = true
  try {
    const res = await getAnnouncements({ page: 1, pageSize: 50 })
    announcements.value = res.data.items || []
    announcementCount.value = res.data.total ?? announcements.value.length
  } catch {
    announcements.value = []
    announcementCount.value = 0
  } finally {
    loadingAnnouncements.value = false
  }
}

const handleCreateAnnouncement = async () => {
  if (!announcementForm.value.title.trim() || !announcementForm.value.content.trim()) return

  try {
    clearMessage('announcements')
    loadingAnnouncements.value = true
    const payload = {
      title: announcementForm.value.title.trim(),
      content: announcementForm.value.content.trim(),
    }
    if (announcementForm.value.targetType === 'specified') {
      payload.receiverUserIDs = announcementForm.value.receiverUserIDs
    }
    await createSystemNotification(payload)
    announcementForm.value = { title: '', content: '', targetType: 'all', receiverUserIDs: [] }
    announcementUserDraft.value = 0
    await loadAnnouncements()
    showMessage('announcements', 'success', '系统公告已发布')
  } catch (e) {
    showMessage('announcements', 'error', e.response?.data?.message || '系统公告发布失败')
  } finally {
    loadingAnnouncements.value = false
  }
}

const handleDeleteAnnouncement = async (id) => {
  try {
    clearMessage('announcements')
    await deleteNotification(id)
    await loadAnnouncements()
    showMessage('announcements', 'success', '系统公告已删除')
  } catch (e) {
    showMessage('announcements', 'error', e.response?.data?.message || '系统公告删除失败')
  }
}

const handleResolveDispute = async (dispute) => {
  const draft = disputeDrafts.value[dispute.ticketID]
  if (!draft) return

  try {
    clearMessage('disputes')
    await resolveDispute(dispute.ticketID, {
      decision: draft.decision,
      refundAmount: draft.refundAmount || 0,
    })
    await loadDisputes()
    showMessage('disputes', 'success', '纠纷已结案')
  } catch (e) {
    showMessage('disputes', 'error', e.response?.data?.message || '纠纷处理失败')
  }
}

onMounted(async () => {
  const results = await Promise.allSettled([
    loadStats(),
    loadReports(),
    loadPostAudits(),
    loadManagedPosts(),
    loadDisputes(),
    loadAnnouncements(),
  ])

  if (results.every(result => result.status === 'rejected')) {
    const firstReason = results.find(result => result.status === 'rejected')?.reason
    showMessage('system', 'error', '无法加载系统数据: ' + (firstReason?.response?.data?.message || firstReason?.message || '权限不足'))
  }

  loading.value = false
  loadingUsers.value = false
})
</script>

<style scoped>
.card h2 {
  font-size: 1rem;
  margin-bottom: 1rem;
}

.muted {
  color: var(--text-secondary);
  margin-top: 0.75rem;
}

.inline-form {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.user-create-form {
  display: grid;
  grid-template-columns: minmax(140px, 1fr) minmax(180px, 1.2fr) minmax(140px, 1fr) minmax(160px, 1fr) auto;
  gap: 0.5rem;
  margin-bottom: 1rem;
}

.inline-form input {
  min-width: 0;
  flex: 1;
}

.inline-form input,
.manager-form select,
.user-create-form select {
  accent-color: var(--primary);
}

input,
select,
textarea {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  font: inherit;
  padding: 0.625rem 0.75rem;
}

.user-create-form input,
.user-create-form select {
  min-width: 0;
}

.credit-cell {
  align-items: flex-start;
  display: flex;
  gap: 0.5rem;
  flex-direction: column;
  min-width: 90px;
}

.credit-cell strong {
  font-size: 1.05rem;
}

.credit-actions {
  display: flex;
  gap: 0.4rem;
}

.credit-mini-button {
  padding: 0.35rem 0.65rem;
}

.credit-up {
  color: #15803d;
}

.credit-down {
  color: #dc2626;
}

.report-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.report-item {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  padding: 1rem;
}

.report-item p {
  color: var(--text-secondary);
  margin: 0.25rem 0;
}

.report-item small {
  color: var(--text-secondary);
}

.report-actions {
  display: flex;
  gap: 0.5rem;
  align-items: center;
}

.toolbar-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.dispute-item {
  align-items: flex-start;
}

.dispute-context {
  color: var(--text-secondary);
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem 0.75rem;
  margin-top: 0.4rem;
}

.dispute-result {
  border-left: 3px solid var(--primary);
  margin-top: 0.75rem;
  padding-left: 0.75rem;
}

.dispute-result p {
  margin: 0.25rem 0;
}

.forum-admin-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-top: 1rem;
}

.forum-admin-item {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  padding: 1rem;
}

.forum-admin-item p {
  color: var(--text-secondary);
  margin: 0.25rem 0;
}

.forum-admin-item small {
  color: var(--text-secondary);
}

.manager-tags,
.manager-form {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.manager-tags {
  margin-top: 0.75rem;
}

.manager-tag {
  align-items: center;
  background: var(--bg);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  display: inline-flex;
  gap: 0.375rem;
  padding: 0.25rem 0.5rem;
}

.manager-tag button {
  background: transparent;
  border: none;
  color: var(--text-secondary);
  cursor: pointer;
  font: inherit;
  line-height: 1;
}

.manager-form {
  align-self: flex-start;
  min-width: 320px;
}

.manager-form select {
  min-width: 0;
  flex: 1;
}

.inline-muted {
  margin-top: 0;
}

.empty-cell {
  text-align: center;
  color: var(--text-secondary);
}

.section-message {
  margin-bottom: 1rem;
}

.credit-form-label {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  font-weight: 600;
}

.credit-form-textarea {
  min-height: 96px;
  resize: vertical;
}

.credit-history-dialog {
  max-width: 560px;
}

.credit-history-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  overflow-y: auto;
  padding-right: 0.25rem;
}

.credit-history-item {
  align-items: center;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  display: flex;
  gap: 1rem;
  justify-content: space-between;
  padding: 0.75rem;
}

.credit-history-item small {
  color: var(--text-secondary);
  display: block;
  font-size: 0.8rem;
  margin-top: 0.25rem;
}

.credit-history-item b {
  white-space: nowrap;
}


@media (max-width: 900px) {
  .user-create-form {
    grid-template-columns: 1fr;
  }

  .forum-admin-item,
  .toolbar-row,
  .manager-form {
    align-items: stretch;
    flex-direction: column;
  }

  .manager-form {
    min-width: 0;
  }
}
</style>

<style scoped>
.system-page { width:100%; max-width:1480px; margin:0 auto; color:#11172a; }
.system-page .stat-cards { grid-template-columns:repeat(4,minmax(0,1fr)); }
.system-page .stat-card { position:relative;overflow:hidden;text-align:left; }
.system-page .stat-card::after { content:'';position:absolute;width:75px;height:75px;right:-32px;bottom:-40px;border-radius:50%;background:#d9d4ff;opacity:.65; }
.system-page .stat-card .number { letter-spacing:-.04em; }
.system-page .card { min-width:0; }
.system-page .card h2 { letter-spacing:-.02em; }
.system-page :is(input,select,textarea) { border:1px solid #e1e3eb; }
.system-page :is(input,select,textarea):focus { border-color:#7463ee;outline:none;box-shadow:0 0 0 4px rgba(105,87,245,.1); }
.system-page .report-item,.system-page .forum-admin-item,.system-page .credit-history-item { border-color:#e8e9ef;border-radius:13px;background:#fafafd; }
.system-page table { min-width:760px; }
.system-page .table-wrapper { width:100%;max-width:100%;overflow-x:auto; }
.system-page .manager-form { width:min(100%,520px);max-width:100%;min-width:0; }
.system-page .manager-form select { width:100%;max-width:100%; }
.system-page th { font-size:.68rem; }
.system-page tr:hover td { background:#faf9ff; }
@media(max-width:1000px){.system-page .stat-cards{grid-template-columns:repeat(2,minmax(0,1fr))}}
@media(max-width:760px){.system-page .stat-cards{gap:.65rem}.system-page .stat-card{padding:1rem}.system-page .card{padding:1rem;border-radius:17px}.system-page .report-item,.system-page .forum-admin-item{flex-direction:column}.system-page .inline-form{align-items:stretch;flex-direction:column}}
</style>

<style scoped>
/* Admin information hierarchy */
.system-page .stat-cards { gap:.75rem; margin:1rem 0 0; }
.system-page .stat-card { padding:1rem 1.1rem; border:1px solid var(--border); border-radius:12px; background:#fff; box-shadow:none; }
.system-page .stat-card::after { display:none; }
.system-page .stat-card .number { color:#342c87; font-size:1.55rem; }
.system-page .stat-card .label { margin-top:.15rem; color:#7d8595; font-size:.72rem; }
.admin-section-heading { margin:2.4rem 0 .8rem; padding-bottom:.8rem; border-bottom:1px solid var(--border); }
.admin-section-heading > span { display:block; margin-bottom:.2rem; color:var(--primary); font-size:.64rem; font-weight:750; letter-spacing:.1em; }
.admin-section-heading h2 { color:#171d2e; font-size:1.18rem; letter-spacing:-.02em; }
.admin-section-heading p { margin-top:.25rem; color:var(--text-secondary); font-size:.78rem; }
.system-page .card { margin-top:.75rem; padding:1.15rem; border:1px solid var(--border); border-radius:12px; box-shadow:none; }
.system-page .admin-section-heading + .card { margin-top:0; }
.system-page .card h2 { margin-bottom:.9rem; color:#252c3d; font-size:.95rem; }
.system-page :is(.inline-form,.user-create-form) { padding:.8rem; border:1px solid #e7e9ee; border-radius:10px; background:#f8f9fb; }
.system-page :is(input,select,textarea) { border-radius:8px; background:#fff; }
.system-page .report-item, .system-page .forum-admin-item, .system-page .credit-history-item { border-radius:9px; background:#fff; }
.system-page .report-item { padding:.9rem; }
.system-page .report-item p { font-size:.82rem; line-height:1.55; }
.system-page th { background:#f6f7f9; color:#6f7788; }
.system-page td { vertical-align:top; }
.system-page .btn { border-radius:8px; }
@media(max-width:1000px){.system-page .user-create-form{grid-template-columns:repeat(2,minmax(0,1fr))}}
@media(max-width:640px){.admin-section-heading{margin-top:1.8rem}.system-page .stat-cards{grid-template-columns:repeat(2,minmax(0,1fr))}.system-page .user-create-form{grid-template-columns:1fr}.system-page :is(.inline-form,.user-create-form){padding:.7rem}}
</style>

<style scoped>
/* Dashboard launchers and focused management dialogs */
.admin-launch-section { margin-top:1.6rem; }
.admin-launch-heading { display:flex; align-items:flex-end; justify-content:space-between; gap:1rem; margin-bottom:.85rem; }
.admin-launch-heading span { display:block; margin-bottom:.2rem; color:var(--primary); font-size:.64rem; font-weight:750; letter-spacing:.1em; }
.admin-launch-heading h2 { color:#171d2e; font-size:1.15rem; letter-spacing:-.02em; }
.admin-launch-heading p { color:var(--text-secondary); font-size:.76rem; }
.admin-launch-grid { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:.75rem; }
.admin-launch-grid > button { min-width:0; display:flex; align-items:center; gap:.8rem; padding:1rem; border:1px solid var(--border); border-radius:12px; color:var(--text); background:#fff; text-align:left; cursor:pointer; transition:border-color .2s,box-shadow .2s; }
.admin-launch-grid > button:hover { border-color:#c8c4ee; box-shadow:0 5px 18px rgba(31,35,55,.055); }
.admin-launch-grid > button > span:nth-child(2) { min-width:0; display:flex; flex:1; flex-direction:column; }
.admin-launch-grid strong { font-size:.88rem; }
.admin-launch-grid small { margin-top:.15rem; overflow:hidden; color:var(--text-secondary); font-size:.7rem; text-overflow:ellipsis; white-space:nowrap; }
.admin-launch-grid b { color:#9aa1af; font-size:.9rem; }
.launch-icon { width:36px; height:36px; display:grid; place-items:center; flex:0 0 auto; border-radius:9px; color:#5145bf; background:#f0effc; font-size:.78rem; font-weight:750; }
.admin-launch-grid > button:nth-child(3n+2) .launch-icon { color:#176c72; background:#eaf7f5; }
.admin-launch-grid > button:nth-child(3n) .launch-icon { color:#315d9a; background:#edf3fb; }

@media(max-width:900px){.admin-launch-grid{grid-template-columns:repeat(2,minmax(0,1fr))}}
@media(max-width:600px){.admin-launch-heading{align-items:flex-start;flex-direction:column}.admin-launch-grid{grid-template-columns:1fr}}
</style>

<style scoped>
/* Announcement management */
.announcement-form { display:flex; flex-direction:column; gap:.75rem; }
.announcement-form textarea { min-height:80px; resize:vertical; }
.announcement-form-actions { display:flex; align-items:flex-end; gap:.75rem; flex-wrap:wrap; }
.announcement-target-label { display:flex; flex-direction:column; gap:.3rem; font-size:.82rem; font-weight:600; }
.announcement-target-label select { min-width:140px; }
.announcement-user-select { display:flex; flex-direction:column; gap:.5rem; flex:1; min-width:200px; }
.announcement-user-chips { display:flex; flex-wrap:wrap; gap:.4rem; }
.announcement-user-picker select { width:100%; }
@media(max-width:640px){.announcement-form-actions{flex-direction:column;align-items:stretch}}
</style>
