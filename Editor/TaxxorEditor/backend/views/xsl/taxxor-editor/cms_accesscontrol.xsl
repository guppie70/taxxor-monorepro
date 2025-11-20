<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

	<xsl:param name="projectid"/>
	<xsl:param name="pageid"/>
	<xsl:param name="resourceid"/>

	<xsl:param name="editorid"/>
	<xsl:param name="outputchanneltype"/>
	<xsl:param name="outputchannelvariantid"/>
	<xsl:param name="outputchannelvariantlanguage"/>


	<xsl:param name="permissions"/>
	<xsl:param name="render-all">true</xsl:param>

	<xsl:variable name="single-quote">'</xsl:variable>
	<xsl:variable name="js-object">
		<xsl:call-template name="render-javascript-object"/>
	</xsl:variable>


	<xsl:output encoding="UTF-8" indent="yes" method="xml" omit-xml-declaration="yes"/>

	<xsl:template match="/">
		<xsl:choose>
			<xsl:when test="$render-all = 'true'">
				<div class="project-properties">
					<xsl:apply-templates select="/configuration/cms_projects/cms_project[@id = $projectid]"/>
				</div>
			</xsl:when>
			<xsl:otherwise>
				<xsl:apply-templates select="/configuration/rbac/accessrecords"/>
			</xsl:otherwise>
		</xsl:choose>


	</xsl:template>

	<xsl:template match="cms_project">

		<div id="access-control">
			<xsl:apply-templates select="/configuration/rbac/accessrecords"/>
		</div>


		<table id="table-acl-template" style="display: none">
			<tr id="[rowid]" class="tr-newaccessrecord">
				<td>
					<select name="usergroupid" class="select-newgroup form-control">
						<option value="--none--">-- select a group --</option>
						<xsl:for-each select="/configuration/rbac/groups/group[name and not(@id = 'all-users' or contains(@id, 'system'))]">
							<xsl:sort select="name"/>
							<option value="{@id}">
								<xsl:value-of select="name"/>
							</option>
						</xsl:for-each>
						<option value="--none--">-- select a user --</option>
						<xsl:for-each select="/configuration/rbac/users/user[not(contains(@id, 'system')) and not(@disabled = 'true')]">
							<xsl:sort select="@id"/>
							<option value="{@id}">
								<xsl:value-of select="@id"/>
							</option>
						</xsl:for-each>
					</select>
				</td>
				<td>
					<select name="roleid" class="select-newrole form-control">
						<option value="--none--">-- select a role --</option>
						<xsl:for-each select="/configuration/rbac/roles/role[not(contains(@id, 'system'))]">
							<xsl:sort select="name"/>
							<option value="{@id}">
								<xsl:value-of select="name"/>
							</option>
						</xsl:for-each>
					</select>
				</td>
				<td>
					<xsl:text> </xsl:text>
				</td>
				<td>
					<button title="Add" class="btn btn-primary btn-xs btn-add-access-record" onclick="addUsersGroupsToPage('{$projectid}', '{$pageid}', '[rowid]', {$js-object})">
						<span class="glyphicon glyphicon-plus">
							<xsl:text> </xsl:text>
						</span>
					</button>
				</td>
			</tr>
		</table>

		<div class="user-group-controls">
			<div>
				<div class="options pull-left">
					<label>
						<input id="input-reset-inheritance" name="input-reset-inheritance" type="checkbox" onclick="changeResourceInheritance('{$projectid}', '{$pageid}', '{$resourceid}', this.checked, {$js-object})">
							<!-- By default set the checkbox to checked state when we are in the project overview page -->
							<xsl:if test="$pageid = 'cms_project-details'">
								<xsl:attribute name="checked">checked</xsl:attribute>
							</xsl:if>

							<xsl:choose>
								<xsl:when test="/configuration/rbac/current-resource/resource[@reset-inheritance = 'true']">
									<xsl:attribute name="checked">checked</xsl:attribute>
								</xsl:when>
							</xsl:choose>
						</input>
						<xsl:text> Reset inheritance</xsl:text>
					</label>
				</div>
				<div class="buttons pull-right">
					<button class="btn btn-primary btn-xs pull-right" id="btn-add-usergroup" onclick="addUsersGroupsToAclLayer('{$projectid}', '{$pageid}')">
						<span class="glyphicon glyphicon-plus">
							<xsl:comment>.</xsl:comment>
						</span>
						<xsl:text> Add user/group</xsl:text>
					</button>
				</div>
			</div>
			<div class="options-extended">
				<xsl:if test="$pageid = 'cms_project-details'">
					<xsl:attribute name="class">hide</xsl:attribute>
				</xsl:if>
				<label class="hide">
					<input id="input-all-outputchannels" name="input-all-outputchannels" type="checkbox">
						<!-- By default set the checkbox to checked state when we are not in the project overview page -->
						<xsl:if test="not($pageid = 'cms_project-details')">
							<xsl:attribute name="checked">checked</xsl:attribute>
						</xsl:if>
					</input>
					<xsl:text> Apply across all output channels </xsl:text>
					<small>(same language)</small>
				</label>
			</div>
		</div>





	</xsl:template>

	<xsl:template match="accessrecords">
		<table id="table-project-acl" class="table table-hover table-condensed table-striped">
			<thead>
				<tr>
					<th>Group / Username</th>
					<th>Role</th>
					<th>Enabled</th>
					<th/>
				</tr>
			</thead>
			<tbody>
				<!-- Inherited role permissions -->
				<xsl:choose>
					<xsl:when test="/configuration/rbac/current-resource/resource[@reset-inheritance = 'true']">
						<!-- When the reset inheritance option is selected, there are no inherited users/groups to show --> </xsl:when>
					<xsl:otherwise>
						<xsl:apply-templates select="/configuration/rbac/inherited-roles/users/user[not(@hide = 'true')]">
							<xsl:sort select="@id"/>
						</xsl:apply-templates>
						<xsl:apply-templates select="/configuration/rbac/inherited-roles/groups/group[not(@hide = 'true')]">
							<xsl:sort select="@id"/>
						</xsl:apply-templates>
					</xsl:otherwise>
				</xsl:choose>

				<xsl:apply-templates select="accessRecord">
					<xsl:sort select="*/@ref"/>
				</xsl:apply-templates>

			</tbody>
		</table>

	</xsl:template>

	<xsl:template match="user | group">
		<xsl:variable name="usergroup-id" select="@id"/>


		<tr class="inherited">
			<td class="text-muted">
				<xsl:choose>
					<xsl:when test="local-name(.) = 'user'">
						<xsl:value-of select="$usergroup-id"/>
					</xsl:when>
					<xsl:when test="/configuration/rbac/groups/group[@id = $usergroup-id]">
						<xsl:value-of select="/configuration/rbac/groups/group[@id = $usergroup-id]/name"/>
					</xsl:when>
					<xsl:otherwise>
						<xsl:value-of select="$usergroup-id"/>
					</xsl:otherwise>
				</xsl:choose>
			</td>
			<td class="text-muted">
				<xsl:for-each select="role">
					<xsl:variable name="role-id" select="text()"/>

					<!-- display this role as an inherited role, if it is not explicitly defined in the access record -->
					<xsl:variable name="draw-role">
						<xsl:choose>
							<xsl:when test="/configuration/rbac/accessrecords/accessRecord[groupRef/@ref = $usergroup-id and roleRef/@ref = $role-id]">no</xsl:when>
							<xsl:when test="/configuration/rbac/accessrecords/accessRecord[userRef/@ref = $usergroup-id and roleRef/@ref = $role-id]">no</xsl:when>
							<xsl:otherwise>yes</xsl:otherwise>
						</xsl:choose>
					</xsl:variable>

					<xsl:if test="$draw-role = 'yes'">
						<xsl:choose>
							<xsl:when test="/configuration/rbac/roles/role[@id = $role-id]">
								<xsl:value-of select="/configuration/rbac/roles/role[@id = $role-id]/name"/>
							</xsl:when>
							<xsl:otherwise>
								<xsl:value-of select="$role-id"/>
							</xsl:otherwise>
						</xsl:choose>
						<xsl:if test="not(position() = last())">
							<xsl:text>, </xsl:text>
						</xsl:if>
					</xsl:if>


				</xsl:for-each>

			</td>
			<td>
				<xsl:text> </xsl:text>
			</td>
			<td class="text-muted">
				<xsl:text>(inherited)</xsl:text>
			</td>
		</tr>

	</xsl:template>

	<xsl:template match="accessRecord">
		<xsl:variable name="user-id" select="userRef/@ref"/>
		<xsl:variable name="group-id" select="groupRef/@ref"/>
		<xsl:variable name="role-id" select="roleRef/@ref"/>
		<xsl:variable name="resource-id" select="resourceRef/@ref"/>

		<xsl:variable name="enabled">
			<xsl:choose>
				<xsl:when test="@disabled = 'true'">false</xsl:when>
				<xsl:otherwise>true</xsl:otherwise>
			</xsl:choose>
		</xsl:variable>

		<xsl:variable name="usergroup-id">
			<xsl:choose>
				<xsl:when test="string-length($user-id) &gt; 0">
					<xsl:value-of select="$user-id"/>
				</xsl:when>
				<xsl:otherwise>
					<xsl:value-of select="$group-id"/>
				</xsl:otherwise>
			</xsl:choose>
		</xsl:variable>

		<tr>
			<td>
				<xsl:choose>
					<xsl:when test="string-length($user-id) &gt; 0">
						<xsl:value-of select="$user-id"/>
					</xsl:when>
					<xsl:when test="/configuration/rbac/groups/group[@id = $group-id]">
						<xsl:value-of select="/configuration/rbac/groups/group[@id = $group-id]/name"/>
					</xsl:when>
					<xsl:otherwise>
						<xsl:value-of select="$group-id"/>
					</xsl:otherwise>
				</xsl:choose>
			</td>
			<td>
				<xsl:choose>
					<xsl:when test="/configuration/rbac/roles/role[@id = $role-id]">
						<xsl:value-of select="/configuration/rbac/roles/role[@id = $role-id]/name"/>
					</xsl:when>
					<xsl:otherwise>
						<xsl:value-of select="$role-id"/>
					</xsl:otherwise>
				</xsl:choose>
			</td>
			<td>
				<input type="checkbox" onclick="enableDisableAccessRecord('{$projectid}', '{$pageid}', '{$usergroup-id}', '{$role-id}', '{$resource-id}', this, {$js-object})">
					<xsl:if test="$enabled = 'true'">
						<xsl:attribute name="checked">checked</xsl:attribute>
					</xsl:if>
				</input>
			</td>
			<td>
				<xsl:choose>
					<xsl:when test="$usergroup-id = 'administrators'">
						<span>&#160;</span>
					</xsl:when>
					<xsl:otherwise>
						<xsl:if test="count(/configuration/rbac/accessrecords/accessRecord) &gt; 0">
							<button title="Delete user/group" class="btn btn-danger btn-xs" onclick="removeUsersGroupsFromPage('{$projectid}', '{$pageid}', '{$usergroup-id}', '{$role-id}', '{$resource-id}', {$js-object})">
								<span class="glyphicon glyphicon-trash">
									<xsl:text> </xsl:text>
								</span>
							</button>
						</xsl:if>
					</xsl:otherwise>
				</xsl:choose>
			</td>
		</tr>

	</xsl:template>



	<xsl:template name="render-option-node">
		<xsl:param name="value"/>
		<xsl:param name="text"/>
		<xsl:param name="default-value"/>
		<xsl:param name="class"/>

		<option value="{$value}">

			<xsl:if test="string-length(normalize-space($default-value)) &gt; 0 and string($value) = string($default-value)">
				<xsl:attribute name="selected">selected</xsl:attribute>
			</xsl:if>
			<xsl:value-of select="$text"/>
		</option>
	</xsl:template>

	<xsl:template name="render-javascript-object">
		<xsl:text>{</xsl:text>
		<xsl:value-of select="concat('editorid:', $single-quote, $editorid, $single-quote, ',')"/>
		<xsl:value-of select="concat('outputchanneltype:', $single-quote, $outputchanneltype, $single-quote, ',')"/>
		<xsl:value-of select="concat('outputchannelvariantid:', $single-quote, $outputchannelvariantid, $single-quote, ',')"/>
		<xsl:value-of select="concat('outputchannelvariantlanguage:', $single-quote, $outputchannelvariantlanguage, $single-quote)"/>
		<xsl:text>}</xsl:text>
	</xsl:template>


</xsl:stylesheet>
