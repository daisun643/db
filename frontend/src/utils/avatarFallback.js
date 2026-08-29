import { reactive } from 'vue'

// 记录加载失败的头像 URL：图片加载出错（404、存储不可用等）时标记，
// 页面自动回退到首字母占位头像，避免出现裂图。
const failedUrls = reactive({})

export const markAvatarFailed = (url) => {
  if (url) failedUrls[url] = true
}

export const isAvatarFailed = (url) => !!url && !!failedUrls[url]

// 头像是否应展示图片：存在 URL 且未加载失败过
export const canShowAvatar = (url) => !!url && !failedUrls[url]
