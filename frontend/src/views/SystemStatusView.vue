<template>
  <div class="page-container">
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
      <h2>数据库连接状态</h2>
      <SectionMessage :message="sectionMessages.system" />
      <div v-if="loading" class="loading">正在检查系统状态...</div>
      <div v-else-if="health" :class="['badge', health.status === 'healthy' ? 'badge-green' : 'badge-red']">
        {{ health.status === 'healthy' ? '数据库连接正常' : '数据库连接异常' }}
      </div>
      <div v-if="health && health.database" class="muted">数据库: {{ health.database }}</div>
    </div>

    <div class="admin-grid">
      <section class="card">
        <h2>角色管理</h2>
        <SectionMessage :message="sectionMessages.roles" />
        <form class="inline-form" @submit.prevent="handleCreateRole">
          <input v-model="roleForm.roleName" type="text" placeholder="角色名称" required />
          <input v-model="roleForm.description" type="text" placeholder="描述" />
          <button class="btn btn-primary" type="submit">创建角色</button>
        </form>

        <div class="role-list">
          <div
            v-for="role in roles"
            :key="roleId(role)"
            :class="['role-row', { active: selectedRoleId === roleId(role) }]"
          >
            <button class="role-item" type="button" @click="selectRole(role)">
              <span>{{ role.roleName }}</span>
              <small>{{ role.description }}</small>
            </button>
            <button class="btn btn-danger" type="button" @click="handleDeleteRole(role)">删除</button>
          </div>
        </div>
      </section>

      <section class="card">
        <h2>权限分配</h2>
        <SectionMessage :message="sectionMessages.permissions" />
        <form class="permission-form" @submit.prevent="handleCreatePermission">
          <input v-model="permissionForm.permissionName" type="text" placeholder="权限名，如 forums.create" required />
          <input v-model="permissionForm.description" type="text" placeholder="说明" />
          <input v-model="permissionForm.resource" type="text" placeholder="资源" />
          <input v-model="permissionForm.action" type="text" placeholder="动作" />
          <button class="btn" type="submit">创建权限</button>
        </form>
        <div v-if="!selectedRoleId" class="muted">选择一个角色后分配权限</div>
        <template v-else>
          <div class="permission-list">
            <label v-for="permission in permissions" :key="permissionId(permission)" class="check-row">
              <input
                v-model="selectedPermissionIds"
                type="checkbox"
                :value="permissionId(permission)"
              />
              <span class="permission-content">
                <span>{{ permission.permissionName }}</span>
                <small>{{ permission.description }}</small>
              </span>
              <button class="btn btn-danger" type="button" @click.prevent.stop="handleDeletePermission(permission)">删除</button>
            </label>
          </div>
          <button class="btn btn-primary" @click="handleAssignPermissions">保存权限</button>
        </template>
      </section>
    </div>

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

    <div class="card">
      <h2>用户与角色</h2>
      <SectionMessage :message="sectionMessages.users" />
      <form class="user-create-form" @submit.prevent="handleCreateUser">
        <input v-model="userForm.username" type="text" placeholder="用户名" required />
        <input v-model="userForm.email" type="email" placeholder="校园邮箱" required />
        <input v-model="userForm.password" type="password" placeholder="初始密码" required />
        <select v-model="userForm.roleIds" multiple>
          <option v-for="role in roles" :key="roleId(role)" :value="roleId(role)">
            {{ role.roleName }}
          </option>
        </select>
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
              <th>操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="user in users" :key="user.userID">
              <td>{{ user.userID }}</td>
              <td>{{ user.username }}</td>
              <td>{{ user.email }}</td>
              <td>{{ user.credit }}</td>
              <td>
                <span :class="['badge', user.status === 'Active' ? 'badge-green' : 'badge-yellow']">
                  {{ user.status || '未知' }}
                </span>
              </td>
              <td>
                <div class="role-checks">
                  <label v-for="role in roles" :key="roleId(role)" class="mini-check">
                    <input
                      :checked="userRoleIds[user.userID]?.includes(roleId(role))"
                      type="checkbox"
                      @change="toggleUserRole(user.userID, roleId(role), $event.target.checked)"
                    />
                    <span>{{ role.roleName }}</span>
                  </label>
                </div>
              </td>
              <td>
                <button class="btn" @click="saveUserRoles(user.userID)">保存</button>
              </td>
            </tr>
            <tr v-if="users.length === 0">
              <td colspan="7" class="empty-cell">暂无数据</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

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
  </div>
</template>

<script setup>
import { ref, onMounted, h } from 'vue'
import {
  approvePostAudit,
  assignPermissionsToRole,
  assignForumManager,
  assignRolesToUser,
  changePostStatus,
  createForum,
  createUser,
  createPermission,
  createRole,
  deletePermission,
  deleteRole,
  getForums,
  getHealth,
  getPermissions,
  getPostAudits,
  getPosts,
  getProducts,
  getReports,
  getDisputes,
  getRoles,
  getUserRoles,
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
const roles = ref([])
const permissions = ref([])
const userRoleIds = ref({})
const reports = ref([])
const postAudits = ref([])
const managedPosts = ref([])
const disputes = ref([])
const disputeDrafts = ref({})
const forumManagerDrafts = ref({})
const postStatusFilter = ref('')
const selectedRoleId = ref(null)
const selectedPermissionIds = ref([])
const loading = ref(true)
const loadingUsers = ref(true)
const messageTimers = new Map()
const sectionMessages = ref({
  system: null,
  roles: null,
  permissions: null,
  forums: null,
  users: null,
  audits: null,
  posts: null,
  disputes: null,
  reports: null,
})

const SectionMessage = (props) => {
  if (!props.message) return null
  return h('div', {
    class: [`${props.message.type}-message`, 'section-message'],
  }, props.message.text)
}

const roleForm = ref({
  roleName: '',
  description: '',
})

const permissionForm = ref({
  permissionName: '',
  description: '',
  resource: '',
  action: '',
})

const forumForm = ref({
  forumName: '',
  description: '',
})

const userForm = ref({
  username: '',
  email: '',
  password: '',
  roleIds: [],
})

const roleId = (role) => role.roleID ?? role.roleId
const permissionId = (permission) => permission.permissionID ?? permission.permissionId

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
  if (usersRes.status === 'fulfilled') users.value = usersRes.value.data
  if (forumsRes.status === 'fulfilled') forums.value = forumsRes.value.data
}

const loadRbac = async () => {
  const [rolesRes, permissionsRes] = await Promise.all([getRoles(), getPermissions()])
  roles.value = rolesRes.data
  permissions.value = permissionsRes.data
  if (selectedRoleId.value) {
    const selected = roles.value.find(role => roleId(role) === selectedRoleId.value)
    if (selected) selectRole(selected)
    else {
      selectedRoleId.value = null
      selectedPermissionIds.value = []
    }
  }
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

const loadUserRoles = async () => {
  const next = {}
  await Promise.all(users.value.map(async (user) => {
    const res = await getUserRoles(user.userID)
    next[user.userID] = res.data.map(role => roleId(role)).filter(Boolean)
  }))
  userRoleIds.value = next
}

const selectRole = (role) => {
  selectedRoleId.value = roleId(role)
  selectedPermissionIds.value = (role.permissions || role.rolePermissions || [])
    .map(item => item.permissionID ?? item.permissionId ?? item.permission?.permissionID ?? item.permission?.permissionId)
    .filter(Boolean)
}

const handleCreateRole = async () => {
  try {
    clearMessage('roles')
    await createRole(roleForm.value)
    roleForm.value = { roleName: '', description: '' }
    await loadRbac()
    showMessage('roles', 'success', '角色已创建')
  } catch (e) {
    showMessage('roles', 'error', e.response?.data?.message || '角色创建失败')
  }
}

const handleDeleteRole = async (role) => {
  if (!window.confirm(`确认删除角色 ${role.roleName}？`)) return

  try {
    clearMessage('roles')
    await deleteRole(roleId(role))
    if (selectedRoleId.value === roleId(role)) {
      selectedRoleId.value = null
      selectedPermissionIds.value = []
    }
    await Promise.all([loadRbac(), loadUserRoles()])
    showMessage('roles', 'success', '角色已删除')
  } catch (e) {
    showMessage('roles', 'error', e.response?.data?.message || '角色删除失败')
  }
}

const handleCreatePermission = async () => {
  try {
    clearMessage('permissions')
    await createPermission({
      permissionName: permissionForm.value.permissionName.trim(),
      description: permissionForm.value.description,
      resource: permissionForm.value.resource,
      action: permissionForm.value.action,
    })
    permissionForm.value = { permissionName: '', description: '', resource: '', action: '' }
    await loadRbac()
    showMessage('permissions', 'success', '权限已创建')
  } catch (e) {
    showMessage('permissions', 'error', e.response?.data?.message || '权限创建失败')
  }
}

const handleDeletePermission = async (permission) => {
  if (!window.confirm(`确认删除权限 ${permission.permissionName}？`)) return

  try {
    clearMessage('permissions')
    await deletePermission(permissionId(permission))
    selectedPermissionIds.value = selectedPermissionIds.value
      .filter(id => id !== permissionId(permission))
    await loadRbac()
    showMessage('permissions', 'success', '权限已删除')
  } catch (e) {
    showMessage('permissions', 'error', e.response?.data?.message || '权限删除失败')
  }
}

const handleAssignPermissions = async () => {
  try {
    clearMessage('permissions')
    await assignPermissionsToRole(selectedRoleId.value, selectedPermissionIds.value)
    await loadRbac()
    showMessage('permissions', 'success', '权限已保存')
  } catch (e) {
    showMessage('permissions', 'error', e.response?.data?.message || '权限保存失败')
  }
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
      roleIds: userForm.value.roleIds,
    })
    userForm.value = { username: '', email: '', password: '', roleIds: [] }
    await loadStats()
    await loadUserRoles()
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

const toggleUserRole = (userId, roleIdValue, checked) => {
  const current = new Set(userRoleIds.value[userId] || [])
  if (checked) current.add(roleIdValue)
  else current.delete(roleIdValue)
  userRoleIds.value = {
    ...userRoleIds.value,
    [userId]: Array.from(current),
  }
}

const saveUserRoles = async (userId) => {
  try {
    clearMessage('users')
    await assignRolesToUser(userId, userRoleIds.value[userId] || [])
    showMessage('users', 'success', '用户角色已保存')
  } catch (e) {
    showMessage('users', 'error', e.response?.data?.message || '用户角色保存失败')
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
    loadRbac(),
    loadReports(),
    loadPostAudits(),
    loadManagedPosts(),
    loadDisputes(),
  ])

  await loadUserRoles().catch(() => {})

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

.admin-grid {
  display: grid;
  grid-template-columns: minmax(280px, 360px) minmax(0, 1fr);
  gap: 1rem;
  margin: 1rem 0;
}

.inline-form {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.permission-form {
  display: grid;
  grid-template-columns: minmax(180px, 1.5fr) minmax(140px, 1fr) minmax(100px, 0.7fr) minmax(100px, 0.7fr) auto;
  gap: 0.5rem;
  margin-bottom: 1rem;
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
.user-create-form select,
.permission-list input {
  accent-color: var(--primary);
}

input,
select {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  font: inherit;
  padding: 0.625rem 0.75rem;
}

.permission-form input {
  min-width: 0;
}

.user-create-form input,
.user-create-form select {
  min-width: 0;
}

.role-list,
.permission-list,
.role-checks {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.role-list {
  margin-top: 1rem;
}

.role-row {
  align-items: stretch;
  display: grid;
  gap: 0.5rem;
  grid-template-columns: minmax(0, 1fr) auto;
}

.role-item {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: transparent;
  color: var(--text);
  cursor: pointer;
  padding: 0.75rem;
  text-align: left;
  width: 100%;
}

.role-row.active .role-item,
.role-item:hover {
  border-color: var(--primary);
  background: var(--bg);
}

.role-item span,
.check-row span {
  display: block;
  font-weight: 600;
}

.role-item small,
.check-row small {
  color: var(--text-secondary);
}

.check-row,
.mini-check {
  display: flex;
  gap: 0.5rem;
  align-items: flex-start;
}

.check-row {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 0.625rem;
}

.permission-content {
  flex: 1;
  min-width: 0;
}

.mini-check {
  font-size: 0.75rem;
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

.post-status-actions {
  flex-wrap: wrap;
  justify-content: flex-end;
  max-width: 320px;
}

.dispute-item {
  align-items: flex-start;
}

.dispute-form {
  display: grid;
  gap: 0.5rem;
  grid-template-columns: minmax(160px, 1fr) 120px auto;
  min-width: min(460px, 100%);
}

.dispute-form input {
  min-width: 0;
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

.success-message {
  background: #dcfce7;
  color: #166534;
  padding: 1rem;
  border-radius: var(--radius);
  margin-bottom: 1rem;
}

.btn-danger {
  border-color: #fecaca;
  color: #b91c1c;
}

.btn-danger:hover {
  background: #fee2e2;
}

@media (max-width: 900px) {
  .admin-grid {
    grid-template-columns: 1fr;
  }

  .permission-form {
    grid-template-columns: 1fr;
  }

  .user-create-form {
    grid-template-columns: 1fr;
  }

  .role-row {
    grid-template-columns: 1fr;
  }

  .forum-admin-item,
  .dispute-form,
  .toolbar-row,
  .manager-form {
    align-items: stretch;
    flex-direction: column;
  }

  .post-status-actions {
    justify-content: flex-start;
    max-width: none;
  }

  .dispute-form {
    display: flex;
    min-width: 0;
  }

  .manager-form {
    min-width: 0;
  }
}
</style>
