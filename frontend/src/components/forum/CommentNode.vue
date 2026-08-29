<template>
  <article :class="['cn', { deleted: isDeleted }]">
    <div
      :class="['cn-avatar', { deleted: isDeleted, clickable: canVisitUser }]"
      :title="canVisitUser ? '查看个人主页' : ''"
      @click="visitUser"
    >{{ isDeleted ? '' : initial }}</div>

    <div class="cn-main">
      <div class="cn-head">
        <span
          :class="['cn-name', { clickable: canVisitUser }]"
          :title="canVisitUser ? '查看个人主页' : ''"
          @click="visitUser"
        >{{ isDeleted ? '用户已删除' : (comment.username || '用户') }}</span>
        <span v-if="!isDeleted && isAuthor" class="cn-badge">楼主</span>
        <span class="cn-time">{{ formatDate(comment.createTime) }}</span>
      </div>

      <p class="cn-text">{{ isDeleted ? '用户已删除该评论' : (comment.content || '') }}</p>

      <div v-if="!isDeleted" class="cn-actions">
        <button class="cn-action" type="button" @click="$emit('reply', comment)">回复</button>
        <button class="cn-action" type="button" @click="$emit('report', comment)">举报</button>
        <button v-if="canDelete" class="cn-action danger" type="button" @click="$emit('delete', comment)">删除</button>
      </div>

      <form
        v-if="replyingTo === comment.commentID"
        class="cn-reply-form"
        @submit.prevent="$emit('submit-reply', comment.commentID)"
      >
        <textarea
          :value="replyText"
          required
          :placeholder="`回复 @${comment.username || '用户'}...`"
          @input="$emit('update-reply', { commentId: comment.commentID, value: $event.target.value })"
        ></textarea>
        <div class="cn-reply-actions">
          <button class="cn-btn ghost" type="button" @click="$emit('cancel-reply')">取消</button>
          <button class="cn-btn primary" type="submit">发送</button>
        </div>
      </form>

      <div v-if="comment.replies?.length" class="cn-children">
        <CommentNode
          v-for="reply in comment.replies"
          :key="reply.commentID"
          :comment="reply"
          :author-id="authorId"
          :replying-to="replyingTo"
          :reply-text="replyText"
          @reply="$emit('reply', $event)"
          @cancel-reply="$emit('cancel-reply')"
          @update-reply="$emit('update-reply', $event)"
          @submit-reply="$emit('submit-reply', $event)"
          @report="$emit('report', $event)"
          @delete="$emit('delete', $event)"
        />
      </div>
    </div>
  </article>
</template>

<script setup>
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'

const props = defineProps({
  comment: { type: Object, required: true },
  authorId: { type: Number, default: null },
  replyingTo: { type: Number, default: null },
  replyText: { type: String, default: '' },
})

defineEmits(['reply', 'cancel-reply', 'update-reply', 'submit-reply', 'report', 'delete'])

const router = useRouter()
const authStore = useAuthStore()

const isDeleted = computed(() => props.comment.status === 'Deleted')
const canVisitUser = computed(() => !isDeleted.value && !!props.comment.userID)
const canDelete = computed(() =>
  props.comment.userID && authStore.user?.userId && props.comment.userID === authStore.user.userId
)
const isAuthor = computed(() =>
  props.authorId && props.comment.userID && props.comment.userID === props.authorId
)
const initial = computed(() => (props.comment.username || '?')[0]?.toUpperCase() || '?')

const visitUser = () => {
  if (canVisitUser.value) {
    router.push(`/user/${props.comment.userID}`)
  }
}

const formatDate = (value) => {
  if (!value) return ''
  return new Date(value).toLocaleString('zh-CN', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}
</script>

<style scoped>
.cn {
  display: flex;
  gap: .65rem;
}

.cn-avatar {
  align-items: center;
  background: linear-gradient(135deg, #4d9bf0, #2f7ee0);
  border-radius: 50%;
  color: #fff;
  display: flex;
  flex: 0 0 auto;
  font-size: .82rem;
  font-weight: 700;
  height: 36px;
  justify-content: center;
  width: 36px;
}

.cn-avatar.clickable {
  cursor: pointer;
}

.cn-avatar.deleted {
  background: #cdd4dc;
}

.cn-main {
  display: grid;
  gap: .3rem;
  min-width: 0;
  flex: 1;
}

.cn-head {
  align-items: center;
  display: flex;
  flex-wrap: wrap;
  gap: .45rem;
}

.cn-name {
  color: #33415c;
  font-size: .86rem;
  font-weight: 700;
}

.cn-name.clickable {
  cursor: pointer;
}

.cn-name.clickable:hover {
  color: #266bc4;
}

.cn-badge {
  background: #eef4ff;
  border-radius: 999px;
  color: #266bc4;
  font-size: .62rem;
  font-weight: 700;
  padding: .1rem .45rem;
}

.cn-time {
  color: #9aa3b3;
  font-size: .72rem;
}

.cn-text {
  color: #11172a;
  font-size: .9rem;
  line-height: 1.6;
  margin: 0;
  white-space: pre-wrap;
  word-break: break-word;
}

.cn.deleted .cn-name,
.cn.deleted .cn-text {
  color: #b9c1c9;
}

.cn.deleted .cn-text {
  font-style: italic;
}

.cn-actions {
  display: flex;
  gap: .25rem;
  margin-top: .1rem;
}

.cn-action {
  background: transparent;
  border: none;
  border-radius: 6px;
  color: #778398;
  cursor: pointer;
  font: inherit;
  font-size: .76rem;
  padding: .2rem .5rem;
}

.cn-action:hover {
  background: #f0f4f9;
  color: #266bc4;
}

.cn-action.danger:hover {
  background: #fdecea;
  color: #b42318;
}

.cn-reply-form {
  background: #fff;
  border: 1px solid #dbe3ee;
  border-radius: 10px;
  display: grid;
  gap: .5rem;
  margin-top: .3rem;
  padding: .6rem;
}

.cn-reply-form textarea {
  border: 1px solid #dbe3ee;
  border-radius: 8px;
  font: inherit;
  font-size: .85rem;
  min-height: 56px;
  padding: .5rem .7rem;
  resize: vertical;
}

.cn-reply-form textarea:focus {
  border-color: #266bc4;
  box-shadow: 0 0 0 3px rgba(38, 107, 196, .12);
  outline: none;
}

.cn-reply-actions {
  display: flex;
  justify-content: flex-end;
  gap: .5rem;
}

.cn-btn {
  border-radius: 999px;
  cursor: pointer;
  font: inherit;
  font-size: .78rem;
  font-weight: 650;
  padding: .35rem .95rem;
}

.cn-btn.primary {
  background: #266bc4;
  border: none;
  color: #fff;
}

.cn-btn.primary:hover {
  background: #1f5aa7;
}

.cn-btn.ghost {
  background: transparent;
  border: 1px solid #dbe3ee;
  color: #5c6b82;
}

.cn-btn.ghost:hover {
  background: #f0f4f9;
}

/* 嵌套回复：只缩进一级（浅底面板），更深层回复平铺在同一面板内，避免深层嵌套越界 */
.cn-children {
  background: #f6f8fb;
  border-radius: 10px;
  display: flex;
  flex-direction: column;
  gap: .8rem;
  margin-top: .35rem;
  padding: .7rem .8rem;
}

.cn .cn .cn-avatar {
  height: 28px;
  width: 28px;
  font-size: .7rem;
}

.cn .cn .cn-children {
  background: transparent;
  border-left: none;
  border-radius: 0;
  margin: 0;
  padding: 0;
}
</style>
