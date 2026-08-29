<template>
  <div class="tab-content">
    <div v-if="loadingMyPosts" class="loading">加载中...</div>
    <div v-else class="post-list tieba-panel personal-feed">
      <ForumPostCard
        v-for="post in myPosts"
        :key="post.postID"
        :post="post"
        mode="mine"
        @open="$emit('open-post', $event)"
        @like="$emit('like', $event)"
        @edit="$emit('edit-post', $event)"
        @delete="$emit('delete-post', $event)"
      />
      <div v-if="myPosts.length === 0" class="empty-state">
        <p>暂无帖子</p>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import ForumPostCard from '../../components/forum/ForumPostCard.vue'
import { getMyPosts } from '../../api'

const emit = defineEmits(['open-post', 'like', 'edit-post', 'delete-post', 'error'])

const myPosts = ref([])
const loadingMyPosts = ref(false)

const loadMyPosts = async () => {
  try {
    loadingMyPosts.value = true
    const res = await getMyPosts()
    myPosts.value = res.data
  } catch (e) {
    emit('error', '无法加载我的帖子: ' + (e.response?.data?.message || e.message))
  } finally {
    loadingMyPosts.value = false
  }
}

const applyPostPatch = (postId, patch) => {
  const target = myPosts.value.find(item => item.postID === postId)
  if (target) {
    Object.assign(target, patch)
  }
}

defineExpose({ reload: loadMyPosts, applyPostPatch })

onMounted(() => {
  loadMyPosts()
})
</script>

<style scoped>
.tab-content {
  min-height: 400px;
  min-width: 0;
}

.post-list {
  margin-top: .9rem;
  border: 1px solid #e4e7ec;
  border-radius: 6px;
  background: #fff;
  overflow: hidden;
}

.empty-state {
  padding: 3rem 1rem;
  color: var(--text-secondary);
  text-align: center;
}
</style>
