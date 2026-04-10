// Keep the shared page state in one object so beginners can see what the UI remembers.
const state = {
	posts: [],
	selectedPostId: null,
	selectedPost: null,
	isEditing: false,
	isLoadingPosts: false,
	isLoadingSelectedPost: false
};

// Store all DOM lookups in one place so the rest of the file can stay focused on behavior.
const elements = {
	statusPanel: document.getElementById("status-panel"),
	refreshPostsButton: document.getElementById("refresh-posts-button"),
	newPostButton: document.getElementById("new-post-button"),
	storageIndicator: document.getElementById("storage-indicator"),
	postListLoading: document.getElementById("post-list-loading"),
	postList: document.getElementById("post-list"),
	postDetails: document.getElementById("post-details"),
	editPostButton: document.getElementById("edit-post-button"),
	deletePostButton: document.getElementById("delete-post-button"),
	postForm: document.getElementById("post-form"),
	postTitleInput: document.getElementById("post-title-input"),
	postBodyInput: document.getElementById("post-body-input"),
	postFormMode: document.getElementById("post-form-mode"),
	savePostButton: document.getElementById("save-post-button"),
	cancelEditButton: document.getElementById("cancel-edit-button"),
	commentForm: document.getElementById("comment-form"),
	commentAuthorInput: document.getElementById("comment-author-input"),
	commentBodyInput: document.getElementById("comment-body-input"),
	saveCommentButton: document.getElementById("save-comment-button")
};

// Start the page after the browser has loaded the HTML.
initializePage();

function initializePage() {
	loadAppInfo();

	elements.refreshPostsButton.addEventListener("click", () => {
		loadPosts(state.selectedPostId);
	});

	elements.newPostButton.addEventListener("click", () => {
		exitEditMode();
		elements.postTitleInput.focus();
	});

	elements.editPostButton.addEventListener("click", () => {
		enterEditMode();
	});

	elements.deletePostButton.addEventListener("click", async () => {
		await deleteSelectedPost();
	});

	elements.cancelEditButton.addEventListener("click", () => {
		exitEditMode();
	});

	elements.postForm.addEventListener("submit", async (event) => {
		event.preventDefault();
		await savePost();
	});

	elements.commentForm.addEventListener("submit", async (event) => {
		event.preventDefault();
		await saveComment();
	});

	loadPosts();
}

async function loadAppInfo() {
	try {
		const appInfo = await sendJsonRequest("/app-info");
		elements.storageIndicator.textContent = `Storage: ${appInfo.storageProvider}`;
	} catch {
		elements.storageIndicator.textContent = "Storage: InMemory";
	}
}

async function loadPosts(preferredPostId) {
	state.isLoadingPosts = true;
	renderPostList();

	try {
		state.posts = await apiGetPosts();

		if (preferredPostId !== undefined) {
			state.selectedPostId = preferredPostId;
		}

		if (state.selectedPostId !== null && !state.posts.some((post) => post.id === state.selectedPostId)) {
			state.selectedPostId = null;
		}

		if (state.selectedPostId === null && state.posts.length > 0) {
			state.selectedPostId = state.posts[0].id;
		}

		renderPostList();

		if (state.selectedPostId === null) {
			state.selectedPost = null;
			renderPostDetails();
			renderCommentFormState();
			return;
		}

		await loadSelectedPost(state.selectedPostId);
	} catch (error) {
		showStatus(error.message, "error");
		state.posts = [];
		state.selectedPostId = null;
		state.selectedPost = null;
		renderPostList();
		renderPostDetails();
		renderCommentFormState();
	} finally {
		state.isLoadingPosts = false;
		renderPostList();
	}
}

async function loadSelectedPost(postId) {
	state.isLoadingSelectedPost = true;
	renderPostDetails();

	try {
		state.selectedPost = await apiGetPost(postId);
		renderPostDetails();
		renderCommentFormState();
	} catch (error) {
		state.selectedPost = null;
		renderPostDetails();
		renderCommentFormState();
		showStatus(error.message, "error");
	} finally {
		state.isLoadingSelectedPost = false;
		renderPostDetails();
	}
}

function renderPostList() {
	elements.postListLoading.hidden = !state.isLoadingPosts;
	elements.postList.innerHTML = "";

	if (!state.isLoadingPosts && state.posts.length === 0) {
		elements.postList.innerHTML = "<li class=\"post-list-item\">No posts yet. Use the form to create the first one.</li>";
		return;
	}

	for (const post of state.posts) {
		const listItem = document.createElement("li");
		listItem.className = "post-list-item";

		if (post.id === state.selectedPostId) {
			listItem.classList.add("selected");
		}

		listItem.tabIndex = 0;
		listItem.innerHTML = `
            <h3>${escapeHtml(post.title)}</h3>
            <p class="post-excerpt">${escapeHtml(post.excerpt)}</p>
            <p class="post-meta">${formatDate(post.createdAtUtc)} · ${post.commentCount} comment(s)</p>
        `;

		listItem.addEventListener("click", async () => {
			state.selectedPostId = post.id;
			exitEditMode(false);
			renderPostList();
			await loadSelectedPost(post.id);
		});

		listItem.addEventListener("keydown", async (event) => {
			if (event.key === "Enter" || event.key === " ") {
				event.preventDefault();
				listItem.click();
			}
		});

		elements.postList.appendChild(listItem);
	}
}

function renderPostDetails() {
	elements.editPostButton.disabled = state.selectedPost === null;
	elements.deletePostButton.disabled = state.selectedPost === null;

	if (state.isLoadingSelectedPost) {
		elements.postDetails.className = "post-details empty-state";
		elements.postDetails.innerHTML = "Loading the selected post...";
		return;
	}

	if (state.selectedPost === null) {
		elements.postDetails.className = "post-details empty-state";
		elements.postDetails.innerHTML = "Choose a post from the list to read it here.";
		return;
	}

	const updatedText = state.selectedPost.updatedAtUtc
		? `Updated ${formatDate(state.selectedPost.updatedAtUtc)}`
		: "Not updated yet";

	const commentsMarkup = state.selectedPost.comments.length === 0
		? "<p class=\"helper-text\">No comments yet. Add the first one with the form below.</p>"
		: `<ul class="comments-list">${state.selectedPost.comments.map((comment) => `
            <li class="comment-item">
                <h3>${escapeHtml(comment.author)}</h3>
                <p class="comment-meta">${formatDate(comment.createdAtUtc)}</p>
                <p class="comment-body">${escapeHtml(comment.body)}</p>
            </li>
        `).join("")}</ul>`;

	elements.postDetails.className = "post-details";
	elements.postDetails.innerHTML = `
        <div class="detail-header">
            <div>
                <h3>${escapeHtml(state.selectedPost.title)}</h3>
                <p class="post-meta">Created ${formatDate(state.selectedPost.createdAtUtc)} · ${updatedText}</p>
            </div>
        </div>
        <p class="post-body">${escapeHtml(state.selectedPost.body)}</p>
        <section aria-labelledby="comment-list-title">
			<h3 id="comment-list-title">Reader comments</h3>
            ${commentsMarkup}
        </section>
    `;
}

function renderCommentFormState() {
	const hasSelectedPost = state.selectedPost !== null;
	elements.saveCommentButton.disabled = !hasSelectedPost;
	elements.commentAuthorInput.disabled = !hasSelectedPost;
	elements.commentBodyInput.disabled = !hasSelectedPost;

	if (!hasSelectedPost) {
		elements.commentAuthorInput.value = "";
		elements.commentBodyInput.value = "";
	}
}

function enterEditMode() {
	if (state.selectedPost === null) {
		return;
	}

	state.isEditing = true;
	elements.postTitleInput.value = state.selectedPost.title;
	elements.postBodyInput.value = state.selectedPost.body;
	elements.postFormMode.textContent = `Editing post ${state.selectedPost.id}. Saving will update the current post.`;
	elements.savePostButton.textContent = "Update post";
	elements.cancelEditButton.hidden = false;
	elements.postTitleInput.focus();
}

function exitEditMode(clearInputs = true) {
	state.isEditing = false;
	elements.postFormMode.textContent = "Create mode is active. Saving will add a new post and open it.";
	elements.savePostButton.textContent = "Save post";
	elements.cancelEditButton.hidden = true;

	if (clearInputs) {
		elements.postForm.reset();
	}
}

async function savePost() {
	const payload = {
		title: elements.postTitleInput.value.trim(),
		body: elements.postBodyInput.value.trim()
	};

	try {
		if (state.isEditing && state.selectedPostId !== null) {
			await apiUpdatePost(state.selectedPostId, payload);
			showStatus("Post updated successfully.", "info");
			exitEditMode();
			await loadPosts(state.selectedPostId);
			return;
		}

		const createdPost = await apiCreatePost(payload);
		showStatus("Post created successfully.", "info");
		exitEditMode();
		await loadPosts(createdPost.id);
	} catch (error) {
		showStatus(error.message, "error");
	}
}

async function deleteSelectedPost() {
	if (state.selectedPostId === null || state.selectedPost === null) {
		return;
	}

	const shouldDelete = window.confirm(`Delete "${state.selectedPost.title}"?`);
	if (!shouldDelete) {
		return;
	}

	try {
		await apiDeletePost(state.selectedPostId);
		showStatus("Post deleted successfully.", "info");
		exitEditMode();
		await loadPosts();
	} catch (error) {
		showStatus(error.message, "error");
	}
}

async function saveComment() {
	if (state.selectedPostId === null) {
		return;
	}

	const payload = {
		author: elements.commentAuthorInput.value.trim(),
		body: elements.commentBodyInput.value.trim()
	};

	try {
		await apiCreateComment(state.selectedPostId, payload);
		elements.commentForm.reset();
		showStatus("Comment added successfully.", "info");
		await loadPosts(state.selectedPostId);
	} catch (error) {
		showStatus(error.message, "error");
	}
}

// The helper functions below keep fetch details in one place,
// so the event handlers above can stay focused on page behavior.

async function apiGetPosts() {
	return await sendJsonRequest("/posts");
}

async function apiGetPost(postId) {
	return await sendJsonRequest(`/posts/${postId}`);
}

async function apiCreatePost(payload) {
	return await sendJsonRequest("/posts", {
		method: "POST",
		body: JSON.stringify(payload)
	});
}

async function apiUpdatePost(postId, payload) {
	return await sendJsonRequest(`/posts/${postId}`, {
		method: "PUT",
		body: JSON.stringify(payload)
	});
}

async function apiDeletePost(postId) {
	await sendJsonRequest(`/posts/${postId}`, {
		method: "DELETE"
	});
}

async function apiCreateComment(postId, payload) {
	return await sendJsonRequest(`/posts/${postId}/comments`, {
		method: "POST",
		body: JSON.stringify(payload)
	});
}

async function sendJsonRequest(url, options = {}) {
	const response = await fetch(url, {
		headers: {
			"Content-Type": "application/json",
			...(options.headers ?? {})
		},
		...options
	});

	// Some endpoints return JSON and some return no content, so read text first and parse only when needed.
	const responseText = await response.text();
	const responseBody = responseText ? JSON.parse(responseText) : null;

	if (!response.ok) {
		const message = responseBody?.message ?? `Request failed with status ${response.status}.`;
		throw new Error(message);
	}

	return responseBody;
}

function showStatus(message, type) {
	elements.statusPanel.textContent = message;
	elements.statusPanel.className = `status-panel ${type}`;
}

function formatDate(value) {
	return new Date(value).toLocaleString();
}

function escapeHtml(value) {
	return value
		.replaceAll("&", "&amp;")
		.replaceAll("<", "&lt;")
		.replaceAll(">", "&gt;")
		.replaceAll('"', "&quot;")
		.replaceAll("'", "&#39;");
}