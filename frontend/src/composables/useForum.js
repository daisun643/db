import { ref } from 'vue'
import { useRouter } from 'vue-router'
import {
  addPostToFavoriteFolder,
  createComment,
  createFavoriteFolder,
  createForum,
  createReport,
  deleteComment,
  deletePost,
  getFavoriteFolders,
  getForums,
  getPost,
  getPostComments,
  joinForum,
  leaveForum,
  likePost,
  unfavoritePost,
  unlikePost,
} from '../api'

// 模块级共享状态：论坛布局与各子页面共用同一份数据与弹窗状态
const forums = ref([])
const favoriteFolders = ref([])
const error = ref(null)
const notice = ref('')

// 帖子详情页（/forums/post/:postId）
const detailLoading = ref(false)
const selectedPost = ref(null)
const comments = ref([])
const commentsLoading = ref(false)
const commentText = ref('')
const commentSubmitting = ref(false)
const replyingTo = ref(null)
const replyText = ref('')

// 发帖 / 编辑 / 举报 / 创建版块
const composerOpen = ref(false)
const editingPost = ref(null)
const reportTarget = ref(null)
const reportReason = ref('')
const forumCreatorOpen = ref(false)
const creatingForum = ref(false)
const forumForm = ref({
  forumName: '',
  description: '',
})

// 收藏夹选择器
const folderPickerOpen = ref(false)
const folderPickerTarget = ref(null)

// 各列表页面注册自己的句柄，用于点赞/收藏后跨列表同步、操作后统一刷新
const feedHandles = new Set()
const registerFeed = (handle) => {
  feedHandles.add(handle)
  return () => feedHandles.delete(handle)
}

const patchMountedPosts = (postId, patch) => {
  for (const handle of feedHandles) {
    handle.applyPostPatch?.(postId, patch)
  }
}

const reloadMountedFeeds = () => {
  for (const handle of feedHandles) {
    handle.reload?.()
  }
}

const loadForums = async () => {
  const res = await getForums()
  forums.value = res.data
}

const loadFavoriteFolders = async () => {
  const res = await getFavoriteFolders()
  favoriteFolders.value = res.data
}

const initialize = async () => {
  try {
    await Promise.all([loadForums(), loadFavoriteFolders()])
  } catch (e) {
    error.value = '无法加载论坛数据: ' + (e.response?.data?.message || e.message)
  }
}

// ---- 版块创建 ----

const openForumCreator = () => {
  error.value = null
  forumCreatorOpen.value = true
}

const closeForumCreator = () => {
  if (creatingForum.value) return
  forumCreatorOpen.value = false
}

const handleCreateForum = async (router) => {
  try {
    creatingForum.value = true
    error.value = null
    const res = await createForum({
      forumName: forumForm.value.forumName.trim(),
      description: forumForm.value.description.trim(),
    })
    forumForm.value = { forumName: '', description: '' }
    forumCreatorOpen.value = false
    notice.value = '版块创建成功。'
    await loadForums()
    if (res.data?.forumID) {
      await router.push(`/forums/board/${res.data.forumID}`)
    }
  } catch (e) {
    error.value = '创建版块失败: ' + (e.response?.data?.message || e.message)
  } finally {
    creatingForum.value = false
  }
}

// ---- 关注 / 取消关注版块 ----
// 版块对象来自共享的 forums 列表，乐观更新后各页面状态自动同步。
const togglingForumJoinId = ref(null)

const handleToggleForumJoin = async (forum) => {
  if (!forum?.forumID || togglingForumJoinId.value) return
  try {
    togglingForumJoinId.value = forum.forumID
    error.value = null
    if (forum.isJoined) {
      const res = await leaveForum(forum.forumID)
      forum.isJoined = false
      forum.memberCount = Math.max(0, (forum.memberCount || 0) - 1)
      notice.value = res.data?.message || '已取消关注。'
    } else {
      const res = await joinForum(forum.forumID)
      forum.isJoined = true
      forum.memberCount = (forum.memberCount || 0) + 1
      notice.value = res.data?.message || '已关注版块。'
    }
  } catch (e) {
    error.value = (forum.isJoined ? '取消关注' : '关注') + '版块失败: ' + (e.response?.data?.message || e.message)
  } finally {
    togglingForumJoinId.value = null
  }
}

// ---- 发帖 ----

const openComposer = () => {
  error.value = null
  composerOpen.value = true
}

const closeComposer = () => {
  composerOpen.value = false
}

const handlePostCreated = (post) => {
  notice.value = post?.status === 'PendingReview'
    ? '帖子已提交审核：内容命中敏感词，暂不会公开展示；可在“我的帖子”查看审核状态。'
    : '帖子发布成功。'
  reloadMountedFeeds()
}

// ---- 点赞 / 收藏 ----

const updatePostLikeState = (postId, isLiked, likeCount) => {
  if (selectedPost.value?.postID === postId) {
    selectedPost.value.isLiked = isLiked
    selectedPost.value.likeCount = likeCount
  }
  patchMountedPosts(postId, { isLiked, likeCount })
}

const updatePostFavoriteState = (postId, isFavorited) => {
  if (selectedPost.value?.postID === postId) {
    selectedPost.value.isFavorited = isFavorited
  }
  patchMountedPosts(postId, { isFavorited })
}

const handleLike = async (post) => {
  try {
    error.value = null
    const wasLiked = post.isLiked
    const res = wasLiked ? await unlikePost(post.postID) : await likePost(post.postID)
    updatePostLikeState(post.postID, res.data.liked, res.data.likeCount)
    notice.value = wasLiked ? '已取消点赞。' : '点赞成功。'
  } catch (e) {
    error.value = (post.isLiked ? '取消点赞' : '点赞') + '失败: ' + (e.response?.data?.message || e.message)
  }
}

const handleFavorite = async (post) => {
  if (post.isFavorited) {
    await unfavoritePost(post.postID)
    updatePostFavoriteState(post.postID, false)
    await loadFavoriteFolders()
    reloadMountedFeeds()
    notice.value = '已取消收藏。'
    return
  }

  if (favoriteFolders.value.length === 0) {
    await createFavoriteFolder({ folderName: '默认收藏夹' })
    await loadFavoriteFolders()
    notice.value = '收藏夹创建成功。'
  }
  folderPickerTarget.value = post
  folderPickerOpen.value = true
}

const handlePickerSaveFolders = async (folderIds) => {
  if (!folderPickerTarget.value || !folderIds?.length) return
  const postId = folderPickerTarget.value.postID
  const results = await Promise.allSettled(
    folderIds.map(folderId => addPostToFavoriteFolder(folderId, postId)),
  )
  const okCount = results.filter(r => r.status === 'fulfilled').length
  if (okCount === 0) {
    const firstError = results[0]?.reason
    error.value = '收藏失败: ' + (firstError?.response?.data?.message || firstError?.message || '未知错误')
    return
  }
  updatePostFavoriteState(postId, true)
  await loadFavoriteFolders()
  closeFolderPicker()
  notice.value = okCount === 1 ? '收藏成功。' : `已收藏到 ${okCount} 个收藏夹。`
}

const closeFolderPicker = () => {
  folderPickerOpen.value = false
  folderPickerTarget.value = null
}

// ---- 帖子删除 / 编辑 ----

const handleDeletePost = async (post) => {
  await deletePost(post.postID)
  reloadMountedFeeds()
}

const openEditPost = async (post) => {
  try {
    error.value = null
    const res = await getPost(post.postID)
    editingPost.value = res.data
  } catch (e) {
    error.value = '无法加载编辑内容: ' + (e.response?.data?.message || e.message)
  }
}

const closeEditPost = () => {
  editingPost.value = null
}

const handleEditorSaved = (updatedPost) => {
  if (selectedPost.value?.postID === updatedPost?.postID) {
    selectedPost.value = updatedPost
  }
  reloadMountedFeeds()
  closeEditPost()
}

// ---- 举报 ----

const openReport = (post) => {
  reportTarget.value = {
    targetType: 'Post',
    typeLabel: '帖子',
    id: post.postID,
    title: post.title,
  }
  reportReason.value = ''
}

const openCommentReport = (comment) => {
  reportTarget.value = {
    targetType: 'Comment',
    typeLabel: '评论',
    id: comment.commentID,
    title: comment.content || '评论内容',
  }
  reportReason.value = ''
}

const handleCreateReport = async () => {
  if (!reportTarget.value) return

  try {
    error.value = null
    await createReport({
      targetType: reportTarget.value.targetType,
      targetID: reportTarget.value.id,
      reason: reportReason.value,
    })
    reportTarget.value = null
    reportReason.value = ''
    notice.value = '举报已提交，我们会尽快核实处理。'
  } catch (e) {
    error.value = '举报失败: ' + (e.response?.data?.message || e.message)
  }
}

// ---- 帖子详情与评论 ----

const loadComments = async () => {
  if (!selectedPost.value) return

  try {
    commentsLoading.value = true
    const res = await getPostComments(selectedPost.value.postID)
    comments.value = res.data
  } catch (e) {
    error.value = '无法加载评论: ' + (e.response?.data?.message || e.message)
  } finally {
    commentsLoading.value = false
  }
}

const loadPostDetail = async (postId) => {
  try {
    detailLoading.value = true
    error.value = null
    const res = await getPost(postId)
    selectedPost.value = res.data
    await loadComments()
  } catch (e) {
    error.value = '无法加载帖子详情: ' + (e.response?.data?.message || e.message)
  } finally {
    detailLoading.value = false
  }
}

const cancelReply = () => {
  replyingTo.value = null
  replyText.value = ''
}

const closePostDetail = () => {
  selectedPost.value = null
  comments.value = []
  commentText.value = ''
  cancelReply()
}

const startReply = (comment) => {
  replyingTo.value = comment.commentID
  replyText.value = `@${comment.username || '用户'} `
}

const handleCreateComment = async (parentCommentId) => {
  if (!selectedPost.value) return

  const content = parentCommentId ? replyText.value : commentText.value
  if (!content.trim()) return

  try {
    commentSubmitting.value = true
    error.value = null
    const res = await createComment(selectedPost.value.postID, {
      content: content.trim(),
      parentCommentID: parentCommentId,
    })
    notice.value = res.data?.status === 'PendingReview'
      ? '评论已提交审核：内容命中敏感词，暂不会公开展示。'
      : '评论发布成功。'
    commentText.value = ''
    cancelReply()
    await loadComments()
    const refreshed = await getPost(selectedPost.value.postID)
    selectedPost.value = refreshed.data
  } catch (e) {
    error.value = '评论失败: ' + (e.response?.data?.message || e.message)
  } finally {
    commentSubmitting.value = false
  }
}

const handleDeleteComment = async (comment) => {
  if (!selectedPost.value) return

  try {
    error.value = null
    await deleteComment(comment.commentID)
    await loadComments()
    const refreshed = await getPost(selectedPost.value.postID)
    selectedPost.value = refreshed.data
  } catch (e) {
    error.value = '删除评论失败: ' + (e.response?.data?.message || e.message)
  }
}

export function useForum() {
  // useForum 始终在组件 setup 中调用，可安全获取 router
  const router = useRouter()

  return {
    // 数据
    forums,
    favoriteFolders,
    error,
    notice,
    loadForums,
    loadFavoriteFolders,
    initialize,
    registerFeed,

    // 版块创建
    forumCreatorOpen,
    creatingForum,
    forumForm,
    openForumCreator,
    closeForumCreator,
    createForumSubmit: () => handleCreateForum(router),

    // 关注 / 取消关注版块
    togglingForumJoinId,
    handleToggleForumJoin,

    // 发帖
    composerOpen,
    openComposer,
    closeComposer,
    handlePostCreated,

    // 点赞 / 收藏
    handleLike,
    handleFavorite,
    folderPickerOpen,
    handlePickerSaveFolders,
    closeFolderPicker,

    // 删除 / 编辑
    handleDeletePost,
    editingPost,
    openEditPost,
    closeEditPost,
    handleEditorSaved,

    // 举报
    reportTarget,
    reportReason,
    openReport,
    openCommentReport,
    handleCreateReport,

    // 详情与评论（详情页使用）
    detailLoading,
    selectedPost,
    comments,
    commentsLoading,
    commentText,
    commentSubmitting,
    replyingTo,
    replyText,
    loadPostDetail,
    closePostDetail,
    handleCreateComment,
    startReply,
    cancelReply,
    handleDeleteComment,
  }
}
