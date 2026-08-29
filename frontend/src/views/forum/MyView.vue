<template>
  <div class="my-page">
    <nav class="my-page-tabs" aria-label="我的内容">
      <router-link
        v-for="entry in entries"
        :key="entry.path"
        :to="entry.path"
        class="my-tab"
        :class="{ active: route.path === entry.path }"
      >{{ entry.label }}</router-link>
    </nav>
    <router-view v-slot="{ Component }">
      <keep-alive :include="['MyPostsView', 'MyCommentsView', 'MyForumsView', 'FavoritesView']">
        <component :is="Component" />
      </keep-alive>
    </router-view>
  </div>
</template>

<script setup>
import { useRoute } from 'vue-router'

const route = useRoute()

const entries = [
  { path: '/forums/my/posts', label: '我的帖子' },
  { path: '/forums/my/comments', label: '我的评论' },
  { path: '/forums/my/forums', label: '我的版块' },
  { path: '/forums/my/favorites', label: '收藏夹' },
]
</script>

<style scoped>
.my-page { min-width: 0; }

.my-page-tabs {
  display: flex;
  gap: .25rem;
  margin-bottom: .9rem;
  padding: .35rem;
  border: 1px solid #e4e7ec;
  border-radius: 12px;
  background: #fff;
}

.my-tab {
  border-radius: 8px;
  padding: .65rem 1rem;
  color: #718096;
  background: transparent;
  cursor: pointer;
  font-size: .8rem;
  text-decoration: none;
}

.my-tab:hover, .my-tab.active {
  color: #266bc4;
  background: #eef4ff;
  font-weight: 700;
}

@media (max-width: 640px) {
  .my-page-tabs { overflow-x: auto; }
  .my-tab { flex: 0 0 auto; }
}
</style>
