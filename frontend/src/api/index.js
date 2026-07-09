import axios from 'axios'

const api = axios.create({
  baseURL: '/api',
  timeout: 10000,
  withCredentials: true,
})

api.interceptors.response.use(
  response => response,
  error => {
    if (error.response?.status === 401) {
      const publicRoutes = ['/login', '/register', '/forgot-password']
      if (!publicRoutes.includes(window.location.pathname)) {
        window.location.href = '/login'
      }
    }
    return Promise.reject(error)
  }
)

export const sendCode = (email) => api.post('/auth/send-code', { email })
export const register = (data) => api.post('/auth/register', data)
export const login = (data) => api.post('/auth/login', data)
export const logout = () => api.post('/auth/logout')
export const forgotPassword = (email) => api.post('/auth/forgot-password', { email })
export const resetPassword = (data) => api.post('/auth/reset-password', data)
export const getCurrentUser = () => api.get('/auth/me')
export const checkRouteAccess = (path) => api.post('/auth/check-route-access', { path })

export const getUsers = () => api.get('/users')
export const getUser = (id) => api.get(`/users/${id}`)
export const createUser = (data) => api.post('/users', data)
export const getProfile = () => api.get('/user/profile')
export const getCreditAdjustments = () => api.get('/user/credit-adjustments')
export const getUserCreditAdjustments = (userId) => api.get(`/user/${userId}/credit-adjustments`)
export const adjustCredit = (data) => api.post('/user/credit/add', data)
export const updateProfile = (data) => api.put('/user/profile', data)
export const uploadAvatar = (file) => {
  const formData = new FormData()
  formData.append('file', file)
  return api.post('/user/avatar', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
}
export const changePassword = (data) => api.post('/user/password', data)
export const getRoles = () => api.get('/rbac/roles')
export const createRole = (data) => api.post('/rbac/roles', data)
export const updateRole = (id, data) => api.put(`/rbac/roles/${id}`, data)
export const deleteRole = (id) => api.delete(`/rbac/roles/${id}`)
export const getPermissions = () => api.get('/rbac/permissions')
export const createPermission = (data) => api.post('/rbac/permissions', data)
export const deletePermission = (id) => api.delete(`/rbac/permissions/${id}`)
export const assignPermissionsToRole = (roleId, permissionIds) =>
  api.post(`/rbac/roles/${roleId}/permissions`, { permissionIds })
export const getUserRoles = (userId) => api.get(`/rbac/users/${userId}/roles`)
export const assignRolesToUser = (userId, roleIds) =>
  api.post(`/rbac/users/${userId}/roles`, { roleIds })

export const getForums = () => api.get('/forums')
export const getForum = (id) => api.get(`/forums/${id}`)
export const createForum = (data) => api.post('/forums', data)
export const updateForum = (id, data) => api.put(`/forums/${id}`, data)
export const assignForumManager = (forumId, userID) => api.post(`/forums/${forumId}/managers`, { userID })
export const removeForumManager = (forumId, userId) => api.delete(`/forums/${forumId}/managers/${userId}`)

export const getPosts = (params = {}) => api.get('/posts', { params })
export const getPost = (id) => api.get(`/posts/${id}`)
export const createPost = (data) => api.post('/posts', data)
export const updatePost = (id, data) => api.put(`/posts/${id}`, data)
export const deletePost = (id) => api.delete(`/posts/${id}`)
export const changePostStatus = (id, data) => api.post(`/posts/${id}/status`, data)
export const likePost = (id) => api.post(`/posts/${id}/like`)
export const unlikePost = (id) => api.delete(`/posts/${id}/like`)
export const getPostComments = (postId) => api.get(`/posts/${postId}/comments`)
export const createComment = (postId, data) => api.post(`/posts/${postId}/comments`, data)
export const deleteComment = (commentId) => api.delete(`/comments/${commentId}`)
export const getMyPosts = () => api.get('/posts/me')
export const getTags = (keyword = '') => api.get('/tags', { params: { keyword } })
export const suggestTags = (data) => api.post('/tags/suggest', data)
export const getFavoriteFolders = () => api.get('/favorite-folders')
export const createFavoriteFolder = (data) => api.post('/favorite-folders', data)
export const updateFavoriteFolder = (folderId, data) => api.put(`/favorite-folders/${folderId}`, data)
export const deleteFavoriteFolder = (folderId) => api.delete(`/favorite-folders/${folderId}`)
export const getFavoriteFolderPosts = (folderId) => api.get(`/favorite-folders/${folderId}/posts`)
export const addPostToFavoriteFolder = (folderId, postId) => api.post(`/favorite-folders/${folderId}/posts/${postId}`)
export const removePostFromFavoriteFolder = (folderId, postId) => api.delete(`/favorite-folders/${folderId}/posts/${postId}`)
export const getPostAudits = () => api.get('/audits/posts')
export const approvePostAudit = (auditId) => api.post(`/audits/posts/${auditId}/approve`)
export const rejectPostAudit = (auditId) => api.post(`/audits/posts/${auditId}/reject`)

export const getProducts = (params = {}) => api.get('/products', { params })
export const getProduct = (id) => api.get(`/products/${id}`)
export const createProduct = (data) => api.post('/products', data)
export const updateProduct = (id, data) => api.put(`/products/${id}`, data)
export const deleteProduct = (id) => api.delete(`/products/${id}`)
export const changeProductStatus = (id, data) => api.post(`/products/${id}/status`, data)
export const getMyProducts = () => api.get('/products/me')
export const getWallet = () => api.get('/wallet/me')
export const depositWallet = (data) => api.post('/wallet/deposit', data)
export const createTransaction = (data) => api.post('/transactions', data)
export const getMyTransactions = () => api.get('/transactions/me')
export const getSalesTransactions = () => api.get('/transactions/sales')
export const payTransaction = (id) => api.post(`/transactions/${id}/pay`)
export const confirmReceipt = (id) => api.post(`/transactions/${id}/confirm-receipt`)
export const cancelTransaction = (id) => api.post(`/transactions/${id}/cancel`)
export const getOrderMessages = (transactionId) => api.get(`/transactions/${transactionId}/messages`)
export const sendOrderMessage = (transactionId, data) => api.post(`/transactions/${transactionId}/messages`, data)
export const getDisputes = () => api.get('/disputes')
export const createDispute = (transactionId, data) => api.post(`/disputes/transactions/${transactionId}`, data)
export const resolveDispute = (id, data) => api.post(`/disputes/${id}/resolve`, data)
export const getReports = (params = {}) => api.get('/reports', { params })
export const createReport = (data) => api.post('/reports', data)
export const reviewReport = (id, data) => api.post(`/reports/${id}/review`, data)
export const getFriends = () => api.get('/friends')
export const getFriendRequests = () => api.get('/friends/requests')
export const createFriendRequest = (data) => api.post('/friends', data)
export const acceptFriendRequest = (id) => api.post(`/friends/${id}/accept`)
export const rejectFriendRequest = (id) => api.post(`/friends/${id}/reject`)
export const deleteFriend = (id) => api.delete(`/friends/${id}`)
export const getMessages = (params = {}) => api.get('/messages', { params })
export const sendMessage = (data) => api.post('/messages', data)
export const markMessageRead = (id) => api.post(`/messages/${id}/read`)
export const markAllMessagesRead = () => api.post('/messages/read-all')
export const getUnreadMessageCount = () => api.get('/messages/unread-count')
export const getNotifications = () => api.get('/notifications')
export const deleteNotification = (id) => api.delete(`/notifications/${id}`)

export const getHealth = () => api.get('/health')

export default api
